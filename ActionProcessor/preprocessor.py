import os
import numpy as np
import pandas as pd
from pathlib import Path
from enum import Enum
from datetime import datetime, timedelta

def init_data_directories(base_dir):
    assert os.path.exists(base_dir), f"Directory does not exist : {base_dir}"
    
    file_path = Path(base_dir)
    assert file_path.parts, f'Invalid directory. Failed to extract file name ({path})'
    
    file_name = file_path.parts[-1]
    json_file_path = os.path.join(base_dir, f'Log_{file_name}.json')
    audio_data_dir = os.path.join(base_dir, 'Audio')
    context_data_dir = os.path.join(base_dir, 'Context')
    image_data_dir = os.path.join(base_dir, 'Image')
    object_data_dir = os.path.join(base_dir, 'Object')
    depth_data_dir = os.path.join(base_dir, 'Depth')
    camera_data_dir = os.path.join(base_dir, 'Camera')
    
    json_output_file_path = os.path.join(base_dir, f'Processed_Log_{file_name}.json')
    # print('')
    
    return json_file_path, audio_data_dir, context_data_dir, image_data_dir, depth_data_dir, \
        camera_data_dir, object_data_dir, json_output_file_path
        

def load_raw_logs(file_index, json_file_path):
    print(f'[{file_index}] Loading json file: "{json_file_path}"')
    data = pd.read_json(json_file_path, convert_dates=False, dtype={'Timestamp':str})
    data['Timestamp'] = data['Timestamp'].apply(lambda x: datetime.strptime(x, '%y%m%d_%H%M%S_%f'))
    action_type_and_ID_bit = data["ActionTypeAndIdBit"].apply(lambda x: decode_action_type_and_id(x))
    data["ActionType"] = action_type_and_ID_bit.apply(lambda x: x[0])
    data["ActionID"] = action_type_and_ID_bit.apply(lambda x: str(x[1]) if x[1] is not None else None)
    data.drop(columns=["ActionTypeAndIdBit"], inplace=True)
    return data
    
def decode_action_type_and_id(bit_val):
    is_continuous = (bit_val > 0)
    action_type = "Continuous" if is_continuous else "Discrete"
    log_id = bit_val if is_continuous else None
    return action_type, log_id

def get_display_timestamp(dt):
    return dt.strftime("%y%m%d_%H%M%S_%f")

def format_duration(duration):
    hours, remainder = divmod(duration, 3600)
    minutes, seconds = divmod(remainder, 60)
    microseconds = int((seconds - int(seconds)) * 1000000)
    return f"000000_{int(hours):02d}{int(minutes):02d}{int(seconds):02d}_{int(microseconds):06d}"

def get_file_type(file_path):
    path = Path(file_path)
    return path.suffix.lower().replace('.', '').strip()


# Reformat to UAD structure
def uad_format_raw_logs(raw_logs_df, relative_base_dir_path):
    uad_formatted_rows = []

    discrete_df = raw_logs_df[raw_logs_df["ActionType"] == "Discrete"]
    continuous_df = raw_logs_df[raw_logs_df["ActionType"] == "Continuous"]
    
    # Convert Discrete actions to UAD-formatted
    for _, row in discrete_df.iterrows():
        update_to_relative_path(row, relative_base_dir_path)
        uad_formatted_rows.append({
            'Name': row['UserAction'],
            'Type': "Discrete",
            'User': row['User'],
            'Intent': row['UserIntent'],
            'StartTime': get_display_timestamp(row['Timestamp']),
            # 'Duration': format_duration(row['Duration']),
            'Duration': None,
            'TriggerSource': row['ActionTriggerSource'],
            'ReferentType': row['ActionReferentType'],
            'ContextType': row['ActionContextType'],
            'ActionContextDescription': None,
            'Data': [{
                'ActionInvokeLocation': row['Location'],
                'ActionInvokeTimestamp': get_display_timestamp(row['Timestamp']),
                'ActionReferentLocation': row['ActionReferentTransform'],
                'ActionReferentName': row['ActionReferentName'],
                'ActionReferentBody': row['ActionReferent'],
                'ActionContext': row['ActionContext']
            }]
        })

    # Convert Continuous actions to UAD-formatted && Merge
    grouped = continuous_df.groupby("ActionID")
    for _, group in grouped:
        group = group.sort_values("Timestamp")
        first_row = group.iloc[0]
        last_row = group.iloc[-1]
        start_time = group["Timestamp"].min()
        end_time = group["Timestamp"].max()
        duration_sec = (end_time - start_time).total_seconds()
        is_audio_input_data = (first_row['ActionTriggerSource'] == 'Microphone')
        
        data_entries = []
        for index, (_, row) in enumerate(group.iterrows()):
            if is_audio_input_data:
                if index == 0: # Keep only the first index to hold the transcribed text (to minimize file size)
                    row['ActionReferent'] = last_row['ActionReferent']
                else:
                    row['ActionReferent'] = None
            update_to_relative_path(row, relative_base_dir_path)
            data_entries.append({
                'ActionInvokeLocation': row['Location'],
                'ActionInvokeTimestamp': get_display_timestamp(row['Timestamp']),
                'ActionReferentLocation': row['ActionReferentTransform'],
                'ActionReferentName': row['ActionReferentName'],
                'ActionReferentBody': row['ActionReferent'],
                'ActionContext': row['ActionContext']
            })

        uad_formatted_rows.append({
            'Name': first_row['UserAction'],
            'Type': "Continuous",
            'User': first_row['User'],
            'Intent': first_row['UserIntent'],
            'StartTime': get_display_timestamp(start_time),
            'Duration': format_duration(duration_sec),
            'TriggerSource': first_row['ActionTriggerSource'],
            'ReferentType': first_row['ActionReferentType'],
            'ContextType': first_row['ActionContextType'],
            'ActionContextDescription': None,
            'Data': data_entries
        })
    
    return pd.DataFrame(uad_formatted_rows)

def update_to_relative_path(data_row, relative_base_dir_path):
    referent = data_row['ActionReferent']
    context = data_row['ActionContext']
    
    # Referent fileName to relative path
    if referent is not None:
        file_type = get_file_type(referent)
        if file_type == 'glb':
            data_row['ActionReferent'] = os.path.join(Path(relative_base_dir_path), "Object", referent)
        elif file_type == 'png':
            data_row['ActionReferent'] = os.path.join(Path(relative_base_dir_path), "Image", referent)
        elif file_type == 'wav':
            data_row['ActionReferent'] = os.path.join(Path(relative_base_dir_path), "Audio", referent)
        else:
            print(f"Unknown file format found ({file_type}) in row : {row}")

    # Context fileName to relative path
    if context is not None:
        data_row['ActionContext'] = os.path.join(Path(relative_base_dir_path), "Image", context)

def assign_short_user_names(final_logs_df):
    dev_cnt = 1
    new_name_map = {}
    for user_name in set(final_logs_df['User']):
        if len(user_name) > 6:
            new_name_map[user_name] = f'Dev{dev_cnt}'
            dev_cnt += 1
    final_logs_df['User'] = final_logs_df['User'].replace(new_name_map)