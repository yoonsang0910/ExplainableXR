from pathlib import Path
import numpy as np
import base64
import requests
import re
import json

def gpt_query_voice_intent_inference(open_ai_client, user_voice_text):
    raw_response = None
    result = 'Unknown'
    
    if user_voice_text is None or user_voice_text=='':
        return None, result
    
    payload = [
        {"role": "system", "content": """You are an expert in analyzing the intention of a spoken word of a user. Given a transcribed speech text of a user, you need to output a summarized short text that best represents the user's intention for that speech. You must output only the answer, no explanation behind it with the answer. For example, if a text "That road is congested all the time, it would be difficult for people to use it as an evacuation route." was given, the plausible output would be "Revealing concerns of the use of the road for emergency scenario"."""},
        {"role": "user", "content": f"""{user_voice_text}"""}
    ]
    for attempt_index in np.arange(1,4):
        try:
            raw_response = open_ai_client.chat.completions.create(
                # model="gpt-4o",
                # model="gpt-4.1-mini",
                model="gpt-4.1-nano",
                messages=payload,
                temperature=0
            ).to_dict()
            result = parse_voice_response(raw_response)
        except Exception as e:
            print(f"--Error occurred during GPT query : {e}. Retrying..({attempt_index}/{3})")
            continue
        break
    # print(f'--{result}')
    
    return raw_response, result

def gpt_query_context_img_to_description(open_ai_client, image_path, user_action, action_intent, action_referent_name):
    img = encode_image(image_path)
    img_type = get_image_type(image_path)
    assert img_type == 'jpg' or img_type =='png', f'Unknown image input file {img_type}.'
    
    headers = {
      "Content-Type": "application/json",
      "Authorization": f"Bearer {open_ai_client.api_key}"
    }

    system_query = """You are an expert in providing a short summarized, but detailed description of an image in text using the provided context. The snapshot is taken to provide the context of a user's interaction, thus the description of the image must be relevant to the action of the user. You will be given an image, the interaction the user is performing, the intent behind the interaction, and the target object the user is interacting with. Note that the description of the image must be made based on the user's interaction context. Only output the description of the image, no sentences such as  'The image shows..' or 'In the context of..' prepended."""
    user_query = f"""User interaction description: {user_action.strip}, Interaction intent description: {action_intent}, {action_referent_name}"""

    payload = {
    # "model": "gpt-4o",
    # model="gpt-4.1-mini",
    "model": "gpt-4.1-nano",
    "messages": [
    {
        "role": "system",
        "content": system_query,
    },
    {
        "role": "user",
        "content": [
        {
            "type": "text",
            "text": user_query
        },
        {
            "type": "image_url",
            "image_url": {
                "url": f"data:image/{img_type};base64,{img}"
          }
        }
      ]
    }
    ]}
    
    raw_response = None
    for attempt_index in np.arange(1,3):
        try:
            raw_response = requests.post("https://api.openai.com/v1/chat/completions", headers=headers, json=payload).json()
            print(f'--Context description query complete ({image_path}).')
            result = parse_img_desc_response(raw_response)
        except Exception as e:
            print(f"--Error occurred during GPT query : {e}. Retrying..({attempt_index}/{3})")
            continue
        break
    # print(f'--{result}')
    
    return raw_response, result

def gpt_query_referent_name(open_ai_client, image_path, user_action):
    img = encode_image(image_path)
    img_type = get_image_type(image_path)
    assert img_type == 'jpg' or img_type =='png', f'Unknown image input file {img_type}.'
    
    headers = {
      "Content-Type": "application/json",
      "Authorization": f"Bearer {open_ai_client.api_key}"
    }

    system_query = """You are an expert in object detection, and classification. You will be provided an image taken from a user's perspective that contains the object to be classified. You will also be provided with a text that describes the user's action toward that object. Return the class (name) of the object user is interacting with, in a string (e.g. "Car") and return "Unknown" when you are uncertain (if the confidence of the classification is lower than 0.5 in the scale of 0 to 1.0) of the object's class. You must return the class of a single object, and if there are more than one, or you are not certain, you must return "Unknown". Return also the confidence value. The example output should be : <Car,0.5> or <Person,0.9> or <Unknown,0.3>"""
    user_query = f"""User action description: {user_action.strip()}"""

    payload = {
    "model": "gpt-4o",
    # model="gpt-4.1-mini",
    # model="gpt-4.1-nano",
    "messages": [
    {
        "role": "system",
        "content": system_query,
    },
    {
        "role": "user",
        "content": [
        {
            "type": "text",
            "text": user_query
        },
        {
            "type": "image_url",
            "image_url": {
                "url": f"data:image/{img_type};base64,{img}"
          }
        }
      ]
    }
    ]}

    raw_response = None
    for attempt_index in np.arange(1,4):
        try:
            raw_response = requests.post("https://api.openai.com/v1/chat/completions", headers=headers, json=payload).json()
            print(f'--Object classification query complete ({image_path}).')
            result = parse_object_response(raw_response)
        except Exception as e:
            print(f"--Error occurred during GPT query : {e}. Retrying..({attempt_index}/{3})")
            continue
        break
    # print(f'--{result}')
    
    return raw_response, result



# def query_llm_agent(open_ai_client, agent_instruct, json_text_data, model="gpt-4.1-mini"):
# def query_llm_agent(open_ai_client, agent_instruct, json_text_data, model="gpt-4.1"):
def query_llm_agent(open_ai_client, agent_instruct, json_text_data, model="gpt-4o"):
    llm_query = [
        {"role": "system", "content": agent_instruct},
        {"role": "user",   "content": json_text_data}
    ]
    raw_response = open_ai_client.chat.completions.create(
        model=model,
        messages=llm_query,
        temperature=0
    )
    return raw_response
            
def query_user_topic_analysis_agent(open_ai_client, aoi_text):
    result = None
    for attempt_index in np.arange(1,4):
        try:
            agent_response = query_llm_agent(open_ai_client,
            agent_instruct="""
            You are an expert at mapping a user's free-text "Topic-of-Interest" to a set of analysis dimensions. The available dimensions are:
            - Spatial  
            - Temporal  
            - User  
            - Action  
            - Intent  
            - Context  
            Given the user's Topic-of-Interest (which may use synonyms or related terms), identify which of these dimensions apply. Rules:

            1. Match on keywords or concepts 
            (e.g. "navigation, locational properties" -> Spatial; 
            "time, duration" -> Temporal; 
            "collaboration" -> User; 
            "gesture, interaction" -> Action;
            "why" or "intent" -> Intent;
            "environment" or "scene" -> Context).  
            2. If multiple dimensions apply, include all of them.  
            3. If none clearly apply, return all six.  
            4. Always return at least one.  
            5. Output ONLY the comma-separated list of dimensions wrapped in angle brackets—no explanations.  
            6. Use the exact labels: Spatial, Temporal, User, Action, Intent, Context.
            
            Example output format is <Spatial, Temporal>""",
            json_text_data=aoi_text, model="gpt-4.1")
            result = parse_agent_response(agent_response)
            result = parse_content_from_response(result)    
        except Exception as e:
            print(f"--Error occurred during GPT query : {e}. Retrying..({attempt_index}/{3})")
            continue
        break
    
    return result

def query_spatial_agent(open_ai_client, json_text, aoi_text):
    result = None
    for attempt_index in np.arange(1,4):
        try:
            agent_response = query_llm_agent(open_ai_client,
            agent_instruct=f"""
            You are a Spatial analysis specialist.  
            Input: the above DATA dictionary of ActionDescription sentences.

            Each sentence contains exactly one "position ([X, Y, Z])" and "EulerAngle rotation ([rX, rY, rZ])" plus a timestamp "at HH:MM:SS AM/PM".

            Task
            1. Extract each action's Position and Rotation from the text.  
            2. Identify up to 10 key spatial insights: clustering of positions, common approach vectors, dwell points, spatial outliers, or movement consistency.  
            3. Note recurring patterns vs. unique events.
            4. If there is any relevant data to this question "{aoi_text}", concentrate on those data
            5. Return each description with detailed human-readable sentence 

            You MUST follow the following format in returning the result.
            For every insight, <Topic of insight|Spatial pattern description|[ActionIDs]>
            Do not add numbering on each insight. I will be parsing the returned data, thus, you must abide by the format, aforementioned.

            You may refer to the context of other information (other than spatial information) but ONLY return Spatial analyses.""",
            json_text_data=json_text)
            result = parse_agent_response(agent_response)
            result = parse_agent_insights(result) 
        except Exception as e:
            print(f"--Error occurred during GPT query : {e}. Retrying..({attempt_index}/{3})")
            continue
        break
    
    return result

def query_temporal_agent(open_ai_client, json_text, aoi_text):
    result = None
    for attempt_index in np.arange(1,4):
        try:
            agent_response = query_llm_agent(open_ai_client,
            agent_instruct=f"""
            You are a Temporal analysis specialist.  
            Input: the above DATA dictionary of ActionDescription sentences.

            Each sentence contains one or more timestamps "at HH:MM:SS AM/PM" and, where present, a duration "lasted for X.Y seconds" or "occurred N times."

            Task
            1. Extract each action's StartTime and Duration from the text.  
            2. Identify up to 10 key temporal insights: peak activity windows, duration outliers, repeating rhythms, concurrency or gaps, event sequences.  
            3. Note recurring patterns vs. unique events.  
            4. If there is any relevant data to this question "{aoi_text}", concentrate on those data
            5. Return each description with a detailed human-readable sentence.

            You MUST follow the following format in returning the result.  
            For every insight, <Topic of insight|Temporal pattern description|[ActionIDs]>  
            Do not add numbering on each insight. I will be parsing the returned data, thus you must abide by the format aforementioned.

            You may refer to the context of other information (other than temporal information) but ONLY return Temporal analyses.""",
            json_text_data=json_text)
            result = parse_agent_response(agent_response)
            result = parse_agent_insights(result) 
        except Exception as e:
            print(f"--Error occurred during GPT query : {e}. Retrying..({attempt_index}/{3})")
            continue
        break
    
    return result

def query_action_agent(open_ai_client, json_text, aoi_text):
    result = None
    for attempt_index in np.arange(1,4):
        try:
            agent_response = query_llm_agent(open_ai_client,
            agent_instruct=f"""
            You are an Action-type analysis specialist.  
            Input: the above DATA dictionary of ActionDescription sentences.

            Each sentence states its action Name and Type (e.g. "performed 'Enter' action" / "discrete-typed", "Voice" / "continuous-typed").

            Task  
            1. Extract each action's Name and Type from the text.  
            2. Identify up to 10 key action insights: most/least common Name+Type combos, frequent sequences, unique action chains.  
            3. Note recurring patterns vs. unique events.  
            4. If there is any relevant data to this question "{aoi_text}", concentrate on those data
            5. Return each description with a detailed human-readable sentence.

            You MUST follow the following format in returning the result.  
            For every insight, <Topic of insight|Action pattern description|[ActionIDs]>
            Do not add numbering on each insight. I will be parsing the returned data, thus you must abide by the format aforementioned.

            You may refer to other information for context, but ONLY return Action analyses.""",
            json_text_data=json_text)
            result = parse_agent_response(agent_response)
            result = parse_agent_insights(result) 
        except Exception as e:
            print(f"--Error occurred during GPT query : {e}. Retrying..({attempt_index}/{3})")
            continue
        break
    
    return result

def query_context_agent(open_ai_client, json_text, aoi_text):
    result = None
    for attempt_index in np.arange(1,4):
        try:
            agent_response = query_llm_agent(open_ai_client,
            agent_instruct=f"""
            You are a Scene-context analysis specialist.  
            Input: the above DATA dictionary of ActionDescription sentences.

            Each sentence contains a free-text context description following "scene with: ..".

            Task  
            1. Extract key context phrases from each description.  
            2. Identify up to 10 key context insights: common environmental features, unique scene elements, context clusters correlated with actions.  
            3. Note recurring patterns vs. unique events.  
            4. If there is any relevant data to this question "{aoi_text}", concentrate on those data
            5. Return each description with a detailed human-readable sentence.

            You MUST follow the following format in returning the result.  
            For every insight, <Topic of insight|Contextual finding description|[ActionIDs]> 
            Do not add numbering on each insight. I will be parsing the returned data, thus you must abide by the format aforementioned.

            You may refer to other information for context, but ONLY return Context analyses.""",
            json_text_data=json_text)
            result = parse_agent_response(agent_response)
            result = parse_agent_insights(result) 
        except Exception as e:
            print(f"--Error occurred during GPT query : {e}. Retrying..({attempt_index}/{3})")
            continue
        break
    
    return result

def query_intent_agent(open_ai_client, json_text, aoi_text):
    result = None
    for attempt_index in np.arange(1,4):
        try:
            agent_response = query_llm_agent(open_ai_client,
            agent_instruct=f"""
            You are an Intent-analysis specialist.  
            Input: the above DATA dictionary of ActionDescription sentences.

            Each sentence includes its Intent (e.g. "Grab Object", "Unknown", "Asking about the manufacturer of the MacBook").

            Task  
            1. Extract each action's Intent from the text.  
            2. Identify up to 10 key intent insights: most/least frequent intents, shifts over time, recurring vs. anomalous intents.  
            3. Note recurring patterns vs. unique events.  
            4. If there is any relevant data to this question "{aoi_text}", concentrate on those data
            5. Return each description with a detailed human-readable sentence.

            You MUST follow the following format in returning the result.  
            For every insight, <Topic of insight|Intent pattern description|[ActionIDs]>
            Do not add numbering on each insight. I will be parsing the returned data, thus you must abide by the format aforementioned.

            You may refer to other information for context, but ONLY return Intent analyses.""",
            json_text_data=json_text)
            result = parse_agent_response(agent_response)
            result = parse_agent_insights(result)  
        except Exception as e:
            print(f"--Error occurred during GPT query : {e}. Retrying..({attempt_index}/{3})")
            continue
        break
    
    return result

def query_user_agent(open_ai_client, json_text, aoi_text):
    result = None
    for attempt_index in np.arange(1,4):
        try:
            agent_response = query_llm_agent(open_ai_client,
            agent_instruct=f"""
            You are a User-analysis specialist.  
            Input: the above DATA dictionary of ActionDescription sentences.

            Each sentence begins with "User (UserID:XYZ).." indicating who performed the action.

            Task  
            1. Extract each action's UserID from the text.  
            2. Identify up to 10 key user insights: who performed most/fewest actions, user-specific behavior patterns, overlaps or comparisons between users, collaborations
            3. Note recurring patterns vs. unique events per user.  
            4. If there is any relevant data to this question "{aoi_text}", concentrate on those data
            5. Return each description with a detailed human-readable sentence.

            You MUST follow the following format in returning the result.  
            For every insight, <Topic of insight|User behavior description|[ActionIDs]>
            Do not add numbering on each insight. I will be parsing the returned data, thus you must abide by the format aforementioned.

            You may refer to other information for context, but ONLY return User analyses.""",
            json_text_data=json_text)
            result = parse_agent_response(agent_response)
            result = parse_agent_insights(result)   
        except Exception as e:
            print(f"--Error occurred during GPT query : {e}. Retrying..({attempt_index}/{3})")
            continue
        break
    
    return result

def query_analyses_coordination_agent(open_ai_client, json_text, aoi_text, full_logs_df):
    result = None
    for attempt_index in np.arange(1,4):
        try:
            agent_response = query_llm_agent(open_ai_client,
            agent_instruct=f"""
            You are an expert integrator of multi-perspective insights on XR session data. You will receive:

            1. The list of analysis dimensions you will receive as the file. The dimensions will only span across ("Spatial", "Temporal", "Action", "User", "Intent", "Context"),
            but not all of them will be included in the analysis. Only one or more may be included.

            2. A JSON object whose top-level keys are analysis dimensions ("Spatial", "Temporal", "Action", "User", "Intent", "Context") and 
            whose values are lists of insight objects. Each insight object contains metadata of :
            - "topic": a short title
            - "insight": a human-readable insight summary analyzed from the specialized analysis agent (keys of the JSON represent each speciality of an agent)
            - "action_ids": the list of original action IDs  

            3. A free-text Topic-of-Interest to guide your focus.

            Your job is to merge, de-duplicate, and prioritize these specialists' insights into up to 10 final, overarching insights.  

            <Merge rules>
            - If there is any data relevant to this question "{aoi_text}", make sure to include them as part of the insights. If there is no relevant data, do NOT forcefully include, or hallucinate.
            - Combine insights across dimensions when they share a theme or reinforce the same finding, while
            conveying their original meanings.
            - Preserve the strongest wording and key details from each.  
            - If one insight subsumes another, keep only the more general (so that multiple insights can be represented with one) or informative one.
            - Group recurring patterns into single entries; don't list near-duplicates.

            <Output requirements>
            - Produce a JSON array under the key "insights", each entry an object with:
            1. "topic": a concise, generalized title
            2. "insight": the merged, human-readable summary tailored to the Topic-of-Interest
            3. "analyses": the list of agents' specialty the insight was derived from  (e.g. ["Spatial","Temporal"])
            4. "action_ids": the list of action IDs where individual specialized agent's insights derived from. 
            (e.g. If the output insight consistitues of insights of Spatial agent with action_ids ["3","1"] and ["0"], and the action_ids of the insights derived from Temporal agent was ["2", "0"], 
            the action_ids of the output will be ["3","1","2","0"])
            - Do not exceed 10 entries; But do not deliberately elongate or hallucinate insights to generate 10 insights. Less than 10 is also fine, as long as it has at least one. Make sure essential insights are not left out.
            - You may draw on all dimensions equally.  
            - Output only the resulting JSON—no explanatory text.""",
            json_text_data=json_text, model="gpt-4.1")
            json_str = parse_agent_response(agent_response)
            json_dict = json.loads(json_str)
            result = reformat_final_llm_insights(full_logs_df, json_dict)
        except Exception as e:
            print(f"--Error occurred during GPT query : {e}. Retrying..({attempt_index}/{3})")
            continue
        break
    
    return result


def encode_image(image_path):
  with open(image_path, "rb") as image_file:
    return base64.b64encode(image_file.read()).decode('utf-8')

def get_image_type(image_path):
    path = Path(image_path)
    return path.suffix.lower().replace('.', '')

def parse_content_from_response(raw_response):
    interest_list = raw_response.strip().strip('<>').strip()
    final_interest_topics = []
    for interest in interest_list.split(','):
        final_interest_topics.append(interest.strip().lower().capitalize().replace('"', ''))
    
    return final_interest_topics

def parse_agent_response(raw_response):
    choices = raw_response.choices
    if choices and len(choices) > 0:
        message = choices[0].message
        if message:
            content = message.content
            if content:
                return content
    return None

def process_content(raw_str):
    val = raw_str.strip().strip('<>').split(',')
    first_value = val[0].strip()
    second_value = float(val[1].strip())

    return (first_value, second_value)

def parse_img_desc_response(raw_response):
    choices = raw_response.get('choices', None)
    if choices and len(choices) > 0:
        message = choices[0].get('message', None)
        if message:
            content = message.get('content', None)
            if content:
                return content
    return 'Unknown'

def parse_object_response(response):
    content = response.get("choices", [{}])[0].get("message", {}).get("content", "")
    if '<' in content and '>' in content:
        return content[content.find('<') + 1:content.find('>')]
    return "<Unknown,0.0>"

# def parse_object_response(raw_response):
#     choices = raw_response.get('choices', None)
#     if choices and len(choices) > 0:
#         message = choices[0].get('message', None)
#         if message:
#             content = message.get('content', None)
#             if content:
#                 return process_content(content)
#     return None

def parse_voice_response(raw_response):
    choices = raw_response.get('choices', None)
    if choices and len(choices) > 0:
        message = choices[0].get('message', None)
        if message:
            content = message.get('content', None)
            if content:
                return content
    return None

def parse_agent_insights(insight_text):
    pattern = r'<([^|]+)\|([^|]+)\|\[([^\]]*)\]>'
    matches = re.findall(pattern, insight_text)

    parsed = []
    for index, (topic, desc, id_list) in enumerate(matches):
        ids = [i.strip() for i in id_list.split(',') if i.strip()]
        parsed.append({
            'topic': topic.strip(),
            'insight': desc.strip(),
            'action_ids': ids
        })
    return parsed

def reformat_final_llm_insights(data_df, merged_llm_insight_dict):
    final_llm_insight_dict = {}
    for cnt, insight in enumerate(merged_llm_insight_dict['insights']):
        timestamps = []
        for insight_id in insight['action_ids']:
            insight_id_int = int(insight_id)
            timestamp = data_df.at[insight_id_int, 'StartTime']
            timestamps.append(timestamp)
        insight['timestamps'] = timestamps
        del insight['action_ids']
        final_llm_insight_dict[f"{cnt+1}"] = insight
        
    return final_llm_insight_dict