# **Explainable XR: Understanding User Behaviors of XR Environments using LLM-assisted Analytics Framework**
Please use the following citation when referencing our work.
```
@article{kim2025explainablexr,
  author={Kim, Yoonsang and Aamir, Zainab and Singh, Mithilesh and Boorboor, Saeed and Mueller, Klaus and Kaufman, Arie E.},
  journal={IEEE Transactions on Visualization and Computer Graphics}, 
  title={Explainable XR: Understanding User Behaviors of XR Environments Using LLM-assisted Analytics Framework}, 
  year={2025},
  volume={31},
  number={5},
  pages={1-11},
  doi={10.1109/TVCG.2025.3549537}}
```

<p align="center">
    <img src="docs/teasor.jpg" width="100%">
</p>

> *Yoonsang Kim, Zainab Aamir, Mithilesh Singh, Saeed Boorboor, Klaus Mueller, Arie E. Kaufman.*<br>
> *IEEE Transactions on Visualization and Computer Graphics (Special Issue via IEEE VR), 2025*<br>
> [[Paper](https://doi.org/10.1109/TVCG.2025.3549537)], [[arXiv](https://arxiv.org/pdf/2501.13778)], [[Video](https://youtu.be/NLh4FkZr7uI)]

In this repository, we provide the three modules of _Explainable XR (EXR)_ : (1) Action Recorder that captures the multimodal behaviors of users in XR environments - VR, AR, MR - following the UAD (User Action Descriptor) format. (2) Action Processor which offline-processes the recorded the data into analyzable form and shape, then, generates LLM-based insights. Lastly, (3) Visual Analytics Interface that enables the resarchers to view the collected XR data on a web-based dashboard.

## Prerequisites
- Anaconda (Verified on conda 24.1.2)
- Unity 2022.3 or later (Verified on 2022.3.25f1 LTS) & Target platform of your choice (e.g., PC, Android, iOS)
- OpenAI API Key ([Link](https://platform.openai.com/settings/profile/api-keys)) & Environment variable set-up ([Link](https://help.openai.com/en/articles/5112595-best-practices-for-api-key-safety))

## Installation
Each module has independent dependencies of its own. Please follow the steps elaborated below, for each module, **sequentially**.

### 1. Action Recorder
We provide Action Recorder as a [UPM package](https://docs.unity3d.com/Manual/upm-ui-local.html) along with sample scenes and scripts on how to record data in AR and VR environments included under its Samples tab. Switch to the your target platform (e.g., PC, Android, iOS), and upon the installation of the package, it will automatically load all the necessary dependencies including, but not limited to `com.unity.cloud.gltfast`, `com.meta.xr.sdk`, `com.unity.nuget.newtonsoft-json`, and `com.unity.mobile.android-logcat`. 
First, create your own Unity project, and import our package through Package Manager (_Window>Package Manager_). Either (1) directly download it on your device and add it via _Install package from disk_ and select _package.json_, or (2) add via _Import package from git URL_ and insert :
```
https://github.com/yoonsang0910/ExplainableXR.git?path=/ActionRecorder/ExplainableXR
```
Once you complete the package installation, there are **remaining crucial steps** : (1) go to `Project Settings` and under _Graphics>Shader Loading>Preloaded Shaders_, set the `Size` to 1, and assign _"Element 0"_ the `glTFShaderVariants` located under _Packages>com.explainable.xr>Runtime>Materials_. Refer to [official Unity glTF Documentation for more](https://docs.unity3d.com/Packages/com.unity.cloud.gltfast@6.12/manual/ProjectSetup.html). (2) Next, if your target platform (e.g., iOS) requires configurations under `Player`, such as Camera Usage Description, and Signing Key/ID, configure them. (3) Go to `XR Plug-in Management` and select your target Plug-in Provider. Make sure you select Oculus if your targetted platform is a Quest device (**NOT** OpenXR). (Optional) Lastly, it's recommended to install our `Samples` (_Window>Package Manager>Packages-Other>Explainable XR>Samples_) which includes the scripts that may help you understand how EXR recording works across platforms.

 #### Samples
- **PC - Unity Editor**: Testbed scene intended for developers to grasp the example usage of EXR in Unity Editor. The example scene include audio recording, discrete, continuous action recording, customization of actions, and Template-based logging scripts.
- **VR Wearable**: Example usecase of EXR for recording VR HMD and controller transforms and interactions. Tested on Quest 3.
- **AR Hand-held**: Example usecase of EXR for recording an AR Hand-held device transforms and touch inputs. Tested on iPad Pro 11".

> [!IMPORTANT]
On your first package installation, you may encounter several pop-ups that requires Unity Editor restart or settings updates. Please refer to the FAQs below, to resolve this. Different selections may lead to unexpected behaviors of EXR.


### 2. Action Processor
As the first step, install the conda environemnt using the command below:
```
git clone https://github.com/yoonsang0910/ExplainableXR.git
cd ExplainableXR/ActionProcessor
conda env create -f exr_conda.yml
conda activate exr
```

We offer two ways to process the recorded data. (1) First is executing the Python script manually, by defining the Analysis-of-Interest (AoI) variable, `user_aoi_query` inside the main Python script (`main.py`), and specifying the directory (`project_root_dir`) that includes the recorded data. Then, executing the main script. In this case, the value assigned to `project_root_dir` will be _"`/Users/yoonsangkim/Desktop/<recorded_data_root_dir_name>`"_ and the final file structure of the module will look like :

```
<recorded_data_root_dir_name>
|---Audio
    |---<wav_audio_file>
|---Camera
    |---<json_file>
|---Confidence  (Optional)
    |---<png_file>
|---Context
    |---<glb_file>
|---Depth
    |---<png_file>
|---Image
    |---<png_file>
|---Object
    |---<glb_file>
|---Output
    |---Checkpoint
        |---insight_input.prefilter.json
        |---insight_input.prefilter.npz
        |---insight_input.postfilter.json
        |---insight_input.merged.json
    |---full_log_data.json
    |---llm_insights.json

```
The `Output` directory contains the output of the Action Processor module. Another way is (2) an end-to-end GUI approach. The researchers are able interact with the Visual Analytics Interface with simple drag and drops of the JSON data files, configuration of on an HTML page, and the internal logic is invoked in the back. Please refer to the Visual Analytics Interface module for details.

### 3. Visual Analytics Interface
_(Documentation & Code Cleanup Coming soon)_

## Frequently Asked Questions
> **Q1.** What should I do, when it says "This project is using the new input system package ~ Do you want to restart" ?<br>
\> EXR uses the New Input System of Unity by default, thus, click, `Yes`. You may override the use of New Input Action if your code has a dependency the old Input System. In the latter case, you may choose `No` also.<br><br>
> **Q2.** I see "Changes to OVRPlugin detected ~ complete the update", what should I do?<br>
\> Choose `Restart Editor`.<br><br>
> **Q3.** I see "Interaction SDK OpenXR Upgrade" pop-up menu, what should I do?<br>
\> Make sure to select `Keep using OVR Hand` as EXR uses OVR for prefabs and scripts, on Meta Quest devices.<br><br>

## Contact
Please utilize the repo's `Issues` tab before reaching out to us via email. It is easier for us (and for the community) to track unexpected behaviors and maintain them.

- Yoonsang Kim - yoonsakim@cs.stonybrook.edu
- Zainab Aamir - zaamir@cs.stonybrook.edu
- Mithilesh Singh - mkssingh@cs.stonybrook.edu