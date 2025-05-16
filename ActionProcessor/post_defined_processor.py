from multiprocessing import cpu_count
from multiprocessing import Pool
from PIL import Image
from datetime import datetime, timedelta
from post_defined_proc_worker import process_AR_referent_inference, process_context_description_task, init_openai_client
from models import open_ai_client, whisper_model, tokenizer, embeddings_model
from llm_query import *
from pathlib import Path
from tqdm import tqdm
import torch
import math
import json
import jsons
import os
import cv2
import re
import imageio.v2 as imageio  # use v2 API
import numpy as np
import pandas as pd
import trimesh

# Post-defined Referents/Intents/ContextDesc (LLM inferred) && ContextPointCloud fields generation
def post_process_data_logs(uad_logs_df, project_root_dir, audio_data_dir, context_data_dir, image_data_dir, object_data_dir):
    print('Updating Post-Defined data fields..')
    
    # Physical Referent (AR) Inference
    infer_ar_referents(image_data_dir, uad_logs_df, num_workers=2)
    
    # PostDefined-types user intent update (e.g., Audio)
    for index, row in uad_logs_df[uad_logs_df['TriggerSource']=='Microphone'].iterrows():
        wav_audio_bytes_file_path = row['Data'][0]['ActionReferentBody']
        text_data = whisper_transcribe(os.path.join(project_root_dir, wav_audio_bytes_file_path)) # Transcribe WAV audio bytes to text 
        _, intent_data,  = gpt_query_voice_intent_inference(open_ai_client, text_data)
        uad_logs_df.at[index, 'Intent'] = str(intent_data).strip()
        for data in uad_logs_df.at[index, 'Data']:
            data['ActionReferentName'] = None
            data['ActionReferentBody'] = text_data
        # # Keep only the first index to hold the transcribed text (to minimize memory/file size)
        # uad_logs_df.at[index, 'Data'][0]['ActionReferentBody'] = text_data
    
    # Action context description generation
    generate_action_context_descriptions(project_root_dir, uad_logs_df, num_workers=2)

def generate_action_context_descriptions(project_root_dir, uad_logs_df, num_workers):
    num_workers = min(cpu_count(), num_workers)
    tasks = prepare_context_description_tasks(project_root_dir, uad_logs_df)
    with Pool(processes=num_workers, initializer=init_openai_client) as pool:
        results = pool.starmap(process_context_description_task, tasks)
    
    for res in results:
        is_success = res["success"]
        if is_success:
            uad_logs_df_index = res["index"]
            description_data = str(res["description"]).strip()
            uad_logs_df.at[uad_logs_df_index, 'ActionContextDescription'] = description_data
            
def prepare_AR_referent_inference(image_data_dir, uad_logs_df):
    tasks = []
    for index, row in uad_logs_df[(uad_logs_df['ReferentType']=='Physical')&(uad_logs_df['TriggerSource']!='Microphone')].iterrows():
        action = row['Name'].strip()
        for data_list_index, referent_data in enumerate(row['Data']):
            referent_data_filename = referent_data['ActionReferentBody']
            if not referent_data_filename:
                continue
            action_referent_file_path = os.path.join(image_data_dir, Path(referent_data_filename).name)
            if os.path.exists(action_referent_file_path):
                tasks.append((index, data_list_index, action, action_referent_file_path))
            else:
                print(f'Missing file. Skipping action referent inference ({action_referent_file_path})')
    return tasks


def whisper_transcribe(audio_wav_bytes_file_path):
    print(f'--Transcribing audio file ({audio_wav_bytes_file_path}).')
    transcribed_text = whisper_model.transcribe(audio_wav_bytes_file_path, language='en', fp16=False, task='transcribe')["text"]
    return transcribed_text.strip()

def infer_ar_referents(image_data_dir, uad_logs_df, num_workers):
    num_workers = min(cpu_count(), num_workers)
    tasks = prepare_AR_referent_inference(image_data_dir, uad_logs_df)
    
    if len(tasks)>0:
        with Pool(processes=num_workers, initializer=init_openai_client) as pool:
            results = pool.starmap(process_AR_referent_inference, tasks)
        
        for res in results:
            is_success = res["success"]
            if is_success:
                uad_logs_df_index = res["index"]
                uad_logs_df_data_list_index = res["data_list_index"]
                llm_inferred_object_class = str(res["referent_name"])
                uad_logs_df.at[uad_logs_df_index, 'Data'][uad_logs_df_data_list_index]['ActionReferentName'] = \
                    llm_inferred_object_class

def prepare_context_description_tasks(project_root_dir, uad_logs_df):
    tasks = []
    for index, row in uad_logs_df.iterrows():
        data_list = row.get('Data', [])
        if not data_list:
            continue

        first_ctx = data_list[0].get('ActionContext')
        last_ctx = data_list[-1].get('ActionContext')
        context_index = 0 if first_ctx else (-1 if last_ctx else None)

        if context_index is None:
            continue

        context_filename = data_list[context_index].get('ActionContext')
        if not context_filename:
            continue

        context_filepath = os.path.join(project_root_dir, context_filename.strip())
        if not os.path.exists(context_filepath):
            print(f'Missing file. Skipping context desc generation ({context_filepath})')
            continue

        user_action = (row.get("Name") or "").strip()
        action_intent = (row.get("Intent") or "").strip()
        action_referent_name = (data_list[context_index].get("ActionReferentName") or "").strip()
        if action_referent_name == "":
            action_referent_name = data_list[context_index].get("ActionReferentBody")
            if action_referent_name is not None: # Audio/Speech text
                action_referent_name = f'Speech transcribed text: {action_referent_name}'
        else:
            action_referent_name = f'Interaction target object name: {action_referent_name}'
        
        tasks.append((index, context_filepath, user_action, action_intent, action_referent_name))
    return tasks


def generate_point_cloud(uad_logs_df, camera_data_dir, image_data_dir, depth_data_dir, context_data_dir, sampling_stride=2):
    for index, row in uad_logs_df.iterrows():
        if row['ContextType'] == "": # when context is null
                continue
        for data_index, data in enumerate(row['Data']):
            original_context_path = Path(data['ActionContext'])
            file_name = original_context_path.name
            file_name_no_ext = original_context_path.stem
            relative_context_data_dir = original_context_path.parent.parent
            new_relative_context_path = None
            
            cam_params_path = os.path.join(camera_data_dir, f"{file_name}.json")
            color_img_path = os.path.join(image_data_dir, file_name)
            depth_img_path = os.path.join(depth_data_dir, file_name)
            context_pointcloud_glb_file_path = f'{file_name_no_ext}.glb'            
            context_pointcloud_glb_path = os.path.join(context_data_dir, context_pointcloud_glb_file_path)
            
            if os.path.exists(cam_params_path) and \
                os.path.exists(color_img_path) and \
                    os.path.exists(depth_img_path):
                        create_point_cloud_glb(cam_params_path, 
                                            color_img_path,
                                            depth_img_path,
                                            context_pointcloud_glb_path, 
                                            sampling_stride)
                        new_relative_context_path = os.path.join(relative_context_data_dir, "Context", context_pointcloud_glb_file_path)
            else:
                print(f"--Skipping point cloud glb due to missing data ({context_pointcloud_glb_path}).")
            
            # Applying change to dataframe
            uad_logs_df.at[index, 'Data'][data_index]['ActionContext'] = new_relative_context_path
        
def create_point_cloud_glb(cam_params_json_path, color_img_path, depth_img_path, output_glb_path, sampling_stride):
    def quaternion_to_rotation_matrix(qx, qy, qz, qw):
        # normalize
        norm = np.sqrt(qx*qx + qy*qy + qz*qz + qw*qw)
        qx, qy, qz, qw = qx/norm, qy/norm, qz/norm, qw/norm
        xx, yy, zz = qx*qx, qy*qy, qz*qz
        xy, xz, yz = qx*qy, qx*qz, qy*qz
        wx, wy, wz = qw*qx, qw*qy, qw*qz
        return np.array([
            [1-2*(yy+zz),   2*(xy - wz),   2*(xz + wy)],
            [  2*(xy + wz), 1-2*(xx+zz),   2*(yz - wx)],
            [  2*(xz - wy),   2*(yz + wx), 1-2*(xx+yy)]
        ], dtype=np.float64)
        
    def load_camera(json_path):
        d = json.load(open(json_path, 'r'))
        fx_full = d['focalLengthX']
        fy_full = d['focalLengthY']
        cx_full = d['principalPointX']
        cy_full = d['principalPointY']
        screenW = d['screenResolution']['x']
        screenH = d['screenResolution']['y']
        far      = d['farClipPlane']
        fov = d['fieldOfView']
        
        # build camera→world directly from Unity’s position+quaternion
        px, py, pz = d['position']['x'], d['position']['y'], d['position']['z']
        qx, qy, qz, qw = (
            d['rotation']['x'], d['rotation']['y'],
            d['rotation']['z'], d['rotation']['w']
        )
        R = quaternion_to_rotation_matrix(qx, qy, qz, qw)
        cam2world = np.eye(4, dtype=np.float64)
        cam2world[:3,:3] = R
        cam2world[:3, 3] = [px, py, pz]
        return fx_full, fy_full, cx_full, cy_full, screenW, screenH, far, fov, cam2world
    
    def load_depth01(path):
        ext = path.lower().split('.')[-1]
        if ext in ('exr',): # .EXR (32bit)
            # EXR → float32 H×W×C (C>=1), R channel is linear01Depth
            arr = imageio.imread(path).astype(np.float32)
            if arr.ndim == 3:
                arr = arr[..., 0]
            return arr  # already 0..1
        else: # .PNG
            im  = Image.open(path)
            arr = np.array(im)
            if arr.ndim == 3:
                arr = arr[...,0]
            arr = arr.astype(np.float32)
            if im.mode == 'L':              # 8-bit
                arr /= 255.0
            elif im.mode.startswith('I;16'): # 16-bit
                arr /= 65535.0
            return arr
    def save_ply(path, pts, cols):
        N = pts.shape[0]
        with open(path,'w') as f:
            f.write("ply\nformat ascii 1.0\n")
            f.write(f"element vertex {N}\n")
            f.write("property float x\nproperty float y\nproperty float z\n")
            f.write("property uchar red\nproperty uchar green\nproperty uchar blue\n")
            f.write("end_header\n")
            for p,c in zip(pts,cols):
                f.write(f"{p[0]:.6f} {p[1]:.6f} {p[2]:.6f} {c[0]} {c[1]} {c[2]}\n")
                
    def save_glb(path, pts, cols):
        """
        Save a point cloud as a .glb (binary GLTF) file.
        pts: Nx3 float32 array
        cols: Nx3 uint8 array
        """
        cloud = trimesh.points.PointCloud(vertices=pts, colors=cols)
        scene = trimesh.Scene([cloud])
        scene.export(path)

    fx_full, fy_full, cx_full, cy_full, screenW, screenH, far, fov_y, cam2world = load_camera(cam_params_json_path)
    
    color_im   = Image.open(color_img_path).convert('RGB')
    color_w, color_h = color_im.size
    color_np   = np.array(color_im)

    depth01_lr = load_depth01(depth_img_path)
    depth_im   = Image.fromarray(depth01_lr, mode='F')
    depth_hr   = depth_im.resize((color_w, color_h), Image.BILINEAR)
    depth01    = np.array(depth_hr, dtype=np.float32)
    depth_m = depth01 * far
    # depth_m = depth01

    # Rescale intrinsics from full-resolution to color img's
    aspect = screenW / screenH
    color_w, color_h = color_im.size
    
    fov_x = 2 * math.degrees(math.atan(math.tan(math.radians(fov_y)/2) * aspect))
    fy = (color_h * 0.5) / math.tan(math.radians(fov_y) / 2)
    fx = (color_w * 0.5) / math.tan(math.radians(fov_x) / 2)
    cx = color_w * 0.5
    cy = color_h * 0.5

    # Sample grid with stride
    xs = np.arange(0, color_w, sampling_stride, dtype=np.int32)
    ys = np.arange(0, color_h, sampling_stride, dtype=np.int32)
    grid_xs, grid_ys = np.meshgrid(xs, ys)
    Z      = depth_m[grid_ys, grid_xs]
    valid  = (Z>0) & (Z<far)
    xv     = grid_xs[valid].astype(np.float32)
    yv     = grid_ys[valid].astype(np.float32)
    zv     = Z[valid]
    dx = (xv + 0.5 - cx) * zv / fx
    dy = - (yv + 0.5 - cy) * zv / fy

    cam_pts = np.vstack([dx, dy, zv, np.ones_like(zv)])  # 4×N
    world   = (cam2world @ cam_pts).T                   # N×4
    pts     = world[:, :3] / world[:, 3:4]             # N×3
    
    # Coodinate system conversion (Unity to glTF)
    # pts = pts @ np.diag([-1.0, 1.0, -1.0])
    pts = pts @ np.diag([-1.0, 1.0, 1.0])
    cols = color_np[grid_ys[valid], grid_xs[valid], :]

    # Save as GLB
    # save_ply(out_ply, pts, cols)
    save_glb(output_glb_path, pts, cols)
    print(f"Generating context pointcloud ({output_glb_path}).")
    
def parse_duration_to_timedelta(time_str):
    m = re.fullmatch(r'\d{6}_(\d{2})(\d{2})(\d{2})_(\d{6})', time_str)
    hours, minutes, seconds, micros = map(int, m.groups())
    return timedelta(
        hours=hours,
        minutes=minutes,
        seconds=seconds,
        microseconds=micros)  

def reformat_data_for_embeddings(entry):
    # User, Action, Intent, Referent, TriggerSrc, Context data
    user = entry.get("User", "Unknown user")
    action = entry.get("Name", "Unknown input type")
    intent = entry.get("Intent", "Unknown intent")
    trigger_src = entry.get("TriggerSource", "Unknown trigger")
    referent_type = entry.get("ReferentType").lower()
    context_type = entry.get("ContextType").lower()
    context_desc = entry.get("ActionContextDescription") or "No scene description provided."
    
    data_type = entry.get("Type").lower()
    data_item = entry.get("Data")
    if data_item is None:
        print("Missing 'Data' field information.. SKipping.")
        return
    data_cnt = len(data_item)
    data_item = data_item[0]
            
    # Spatial data
    location = data_item.get("ActionReferentLocation")
    location_text = ""
    if location is not None:
        location_data = location.split(',')
        pos_text = [float(s) for s in location_data[:3]]
        rot_text = [float(s) for s in location_data[3:]]
        location_text = f'The action took place at position ({pos_text}) with EulerAngle rotation ({rot_text}).'  
    
    # Temporal data
    start_time_str = entry.get("StartTime")
    start_time_dt = datetime.strptime(start_time_str, "%y%m%d_%H%M%S_%f")
    start_time = f'at {start_time_dt.strftime("%I:%M:%S%p").lstrip("0")}'
    duration = entry.get("Duration")
    duration_text = ""
    if duration is not None:
        duration_str = f'{parse_duration_to_timedelta(duration).total_seconds():.1f}'
        duration_text = f' and lasted for {duration_str} seconds'
    
    referent_name = data_item.get("ActionReferentName") or "an unidentified object"
    referent_body = data_item.get("ActionReferentBody") or ""
    action_type_text = f'Also, this {data_type}-typed action occurred {data_cnt} { "time" if data_cnt == 1 else "times" },'
    
    if trigger_src == "Microphone":
        full_action_desc = f"""User (UserID:{user}) performed "{action}" action with the intent of "{intent}" via {trigger_src}. 
        The user said "{referent_body}" in a {context_type} Unity scene with: "{context_desc}"."""
    else:    
        full_action_desc = f"""User (UserID:{user}) performed "{action}" action with the intent of "{intent}" via {trigger_src}. 
        The action was intended towards a {referent_type} referent, named "{referent_name}", under a {context_type} scene context of: "{context_desc}"."""
              
    return f'{full_action_desc}. {action_type_text} {start_time}{duration_text}. {location_text}'
    
def compute_embeddings(text):
    inputs = tokenizer(text, return_tensors="pt", truncation=True, padding=True)
    with torch.no_grad():
        return embeddings_model(**inputs).last_hidden_state.squeeze(0)  # [tokens, 768]

# Late interaction scoring (ColBERT's)
def compute_late_interaction_score(embed1, embed2):
    sim_matrix = embed1 @ embed2.T  # [embed1_tokens, embed2_tokens]
    return sim_matrix.max(dim=1).values.sum().item()

def get_top_k_embed_indices(score_list, top_k):
    scores = np.array(score_list)
    top_indices = scores.argsort()[::-1][:top_k]
    return top_indices

def filter_less_relevants(df, bottom_percent=0.25):
    cutoff = df["Score"].quantile(bottom_percent) # Bottom 25% 
    return df[df["Score"] > cutoff].sort_values("Score", ascending=False)

def cosine_similarity(vec1, vec2):
    return np.dot(vec1, vec2) / (np.linalg.norm(vec1) * np.linalg.norm(vec2))

def compute_knee_index(desc_sorted_similarity_df, threshold_factor=1.0):
    ys = desc_sorted_similarity_df["Score"].values
    diffs = np.round(np.abs(np.diff(ys)), 6)
    q1, q3 = np.percentile(diffs, [25, 75])
    iqr   = q3 - q1
    threshold = q3 + threshold_factor * iqr
    
    for index, val in enumerate(diffs):
        if val > threshold:
            return index + 1
    return len(desc_sorted_similarity_df)