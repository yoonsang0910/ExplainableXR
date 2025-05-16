open_ai_client = None
whisper_model = None
tokenizer = None
embeddings_model = None

def init_models():
    try:
        from openai import OpenAI
        from transformers import AutoTokenizer, AutoModel
        import whisper
        import warnings
        warnings.filterwarnings("ignore", category=FutureWarning, module="huggingface_hub")

        global open_ai_client, whisper_model, tokenizer, embeddings_model
        open_ai_client = OpenAI() # only for the Main process
        whisper_model = whisper.load_model("base.en") # tiny.en
        embeddings_model_name = "colbert-ir/colbertv2.0"
        tokenizer = AutoTokenizer.from_pretrained(embeddings_model_name)
        embeddings_model = AutoModel.from_pretrained(embeddings_model_name)
        _ = embeddings_model.eval()
    except Exception as e:
        print(f"Error initializing models ({e})")
        return
    
    print("Models initialized and loaded succesfully.")