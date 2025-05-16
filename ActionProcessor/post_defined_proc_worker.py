from llm_query import gpt_query_referent_name, gpt_query_context_img_to_description

open_ai_client = None

def init_openai_client():
    global open_ai_client
    from openai import OpenAI
    open_ai_client = OpenAI()
    
def process_AR_referent_inference(index, data_list_index, action, action_referent_file_path):
    try:
        _, result = gpt_query_referent_name(open_ai_client, action_referent_file_path, action)
        return {
            "index": index,
            "data_list_index": data_list_index,
            "referent_name": result,
            "success": True
        }
    except Exception as e:
        print(f"--GPT AR Referent Inference Error ({e})")
        return {
            "index": index,
            "data_list_index": data_list_index,
            "error": str(e),
            "success": False
        }
        
def process_context_description_task(index, image_path, user_action, intent, referent_name):
    try:
        _, desc = gpt_query_context_img_to_description(open_ai_client, image_path, user_action, intent, referent_name)
        return {
            "index": index,
            "description": desc,
            "success": True
        }
    except Exception as e:
        return {
            "index": index,
            "error": str(e),
            "success": False
        }