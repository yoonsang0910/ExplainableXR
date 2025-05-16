from datetime import datetime, timedelta
from matplotlib import pyplot as plt
from pathlib import Path
from llm_query import query_spatial_agent, query_temporal_agent, query_action_agent, \
    query_context_agent, query_intent_agent, query_user_agent, \
        query_user_topic_analysis_agent, query_analyses_coordination_agent
import multiprocessing
import numpy as np
import pandas as pd
import models
import json
import os
import re
import sys
os.environ["TOKENIZERS_PARALLELISM"] = "false"

def init_data_dir_list(root_dir):
    pattern = re.compile(r'^Log_\d{6}_\d{6}\.json$')
    matching_directories = []
    for dirpath, _, filenames in os.walk(root_dir):
        for filename in filenames:
            if pattern.match(filename):
                matching_directories.append(dirpath)
                break
    print(f'Initializing base project directory ({root_dir})..\n')
    
    return matching_directories

if __name__ == "__main__":
    # user_aoi_query = "What was user doing when they placed AR memos?" # AR
    user_aoi_query = "What was the most frequent color and geometry of the interacted objects?" # VR
    # user_aoi_query = "What topics did users mostly discuss about?"
    # user_aoi_query = "When did hmd interaction happen"
    # user_aoi_query = "When did user do during the session?"
    
    # project_root_dir = r"/Users/yoonsangkim/Desktop/250514_235724" # AR
    project_root_dir = r"/Users/yoonsangkim/Desktop/250514_215335" # VR
    
    if not os.path.exists(project_root_dir):
        print(f"Directory not found ({project_root_dir}).")
        sys.exit()
    
    multiprocessing.set_start_method("spawn", force=True)
    models.init_models()
    
    from post_defined_processor import *
    from preprocessor import *
    
    final_logs_df = pd.DataFrame([])
    project_output_dir = os.path.join(project_root_dir, "Output")
    project_output_checkpoint_dir = os.path.join(project_root_dir, "Output", "Checkpoint")
    os.makedirs(project_output_dir, exist_ok=True)
    os.makedirs(project_output_checkpoint_dir, exist_ok=True)
    data_dir_list = init_data_dir_list(project_root_dir)
    
    # region Load Data & Process 
    # (Load, convert recorded to data UAD format, and process post-defined data)
    for file_index, data_dir in enumerate(data_dir_list):
        # Retrieve directories & path
        json_file_path, audio_data_dir, context_data_dir, image_data_dir, depth_data_dir, \
            camera_data_dir, object_data_dir, json_output_file_path = init_data_directories(data_dir)
        relative_base_dir_path = Path(json_file_path).parent.relative_to(project_root_dir)

        # Load json files in project dir.
        raw_logs_df = load_raw_logs(file_index+1, json_file_path)
        
        # Reformat json files to UAD format
        uad_logs_df = uad_format_raw_logs(raw_logs_df, relative_base_dir_path) 
        
        # Update PostDefined fields using LLM
        post_process_data_logs(uad_logs_df, project_root_dir, audio_data_dir, context_data_dir, image_data_dir, object_data_dir)
        
        # Generate Action context pointcloud (Default sampling stride=2; Original pixel-to-pixel is stride=1)
        generate_point_cloud(uad_logs_df, camera_data_dir, image_data_dir, depth_data_dir, \
            context_data_dir, sampling_stride=2)
        
        final_logs_df = pd.concat([final_logs_df, uad_logs_df], ignore_index=True).reset_index(drop=True)
        print('')
    #endregion
    
    # region Assign short user name for readability of the dashboard (Optional)
    assign_short_user_names(final_logs_df)
    # endregion

    # region Save Full Data Log
    final_logs_df_file_path = os.path.join(project_output_dir, f'full_log_data.json')
    final_logs_df = final_logs_df.sort_values(by='StartTime').reset_index(drop=True) # Sort rows by Timestamp (since the data merges all users, it's necessary)
    final_logs_df.to_json(final_logs_df_file_path, orient='records', indent=4)
    # endregion

    # region Generate Textual Desc & Compute Embeddings
    print(f'Generating Vector Embeddings..')
    
    # Compute embeddings of AoI query
    aoi_query_embeds = compute_embeddings(user_aoi_query)
    
    # Compute embeddings of recorded data
    action_desc_list = []
    embeds_list = []
    index_list = []
    score_list = []
    insight_logs_df = pd.DataFrame([])
    for df_index, log_row in final_logs_df.iterrows():
        action_desc = reformat_data_for_embeddings(log_row)
        embeds = compute_embeddings(action_desc)
        score = compute_late_interaction_score(aoi_query_embeds, embeds)
        action_desc_list.append(action_desc)
        embeds_list.append(embeds.cpu().numpy())
        score_list.append(np.round(score,2))
        index_list.append(df_index)
    
    insight_logs_df['ActionDescription'] = action_desc_list
    insight_logs_df['Score'] = score_list
    insight_logs_df.index = index_list
    #endregion
    
    # region Save LLM Insight Generation Input
    insight_logs_df_file_path = os.path.join(project_output_checkpoint_dir, f'insight_input.prefilter.json') # Pre-AoI-relevant data filter
    insight_logs_df.to_json(insight_logs_df_file_path, orient='index', indent=4)
    np.savez_compressed(os.path.join(project_output_checkpoint_dir, "insight_input.prefilter.npz"), *embeds_list)
    
    # Remove bottom 25% of the rows (lower means irrelevant); Filter more aggresively if only interested in strongly AoI-relevant rows
    # Increase the 'bottom_percent' (up to 1.0) for more aggresive filtering => shortened time for processing
    filtered_insight_logs_df_file_path = os.path.join(project_output_checkpoint_dir, f'insight_input.postfilter.json') # Post-AoI-relevant data filter
    filtered_insight_logs_df = filter_less_relevants(insight_logs_df, bottom_percent=0.25)
    filtered_insight_logs_df.to_json(filtered_insight_logs_df_file_path, orient='index', indent=4)
    print('--Complete.')
    # endregion
    
    # region Generate LLM Insight & Save
    print(f'\nGenerating LLM Insights..')
    llm_input_data_dict = filtered_insight_logs_df.to_dict(orient='index')
    llm_input_data_str = json.dumps(llm_input_data_dict)
    
    target_agents = query_user_topic_analysis_agent(models.open_ai_client, user_aoi_query) # Filter agent list (to minimize time & costs)
    agent_insights = {}
    print(f'--Coordinating multi-agent ({", ".join(target_agents)}) insights.')
    for agent in target_agents: # multi-agent query
        if agent == 'Spatial':
            agent_insights[agent] = query_spatial_agent(models.open_ai_client, llm_input_data_str, user_aoi_query)
        elif agent == 'Temporal':
            agent_insights[agent] = query_temporal_agent(models.open_ai_client, llm_input_data_str, user_aoi_query)
        elif agent == 'Action':
            agent_insights[agent] = query_action_agent(models.open_ai_client, llm_input_data_str, user_aoi_query)
        elif agent == 'Intent':
            agent_insights[agent] = query_intent_agent(models.open_ai_client, llm_input_data_str, user_aoi_query)
        elif agent == 'User':
            agent_insights[agent] = query_user_agent(models.open_ai_client, llm_input_data_str, user_aoi_query)
        elif agent == 'Context':
            agent_insights[agent] = query_context_agent(models.open_ai_client, llm_input_data_str, user_aoi_query)
    
    with open(os.path.join(project_output_checkpoint_dir, f'insight_input.merged.json'), 'w') as json_file:
        json.dump(agent_insights, json_file, indent=2)
    
    # Merge & Coordinate agent answers
    llm_insight_merged_str = json.dumps(agent_insights)
    llm_insight_merged_dict = query_analyses_coordination_agent(models.open_ai_client, llm_insight_merged_str, user_aoi_query, final_logs_df)
    
    # Save Final LLM Insights
    with open(os.path.join(project_output_dir, f'llm_insights.json'), 'w') as json_file:
        json.dump(llm_insight_merged_dict, json_file, indent=2)
    print('--Complete.')
    # endregion
    
    print(f'\nAction Processing Complete!')
    
