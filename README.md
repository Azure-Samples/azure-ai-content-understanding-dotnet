# Azure AI Content Understanding Samples (.NET 8 Console App)

Welcome! Content Understanding is a solution that analyzes and comprehends various media content—including **documents, images, audio, and video**—and transforms it into structured, organized, and searchable data.

Content Understanding is now a Generally Available (GA) service with the release of the `2025-11-01` API version.

- The samples in this repository default to the latest GA API version: `2025-11-01`.
- We will provide more samples for new functionalities in the GA API versions soon. For details on the updates in the current GA release, see the [Content Understanding What's New Document page](https://learn.microsoft.com/en-us/azure/ai-services/content-understanding/whats-new).
- As of November 2025, the `2025-11-01` API version is now available in a broader range of [regions](https://learn.microsoft.com/en-us/azure/ai-services/content-understanding/language-region-support).
- To access sample code for version `2025-05-01-preview`, please check out the corresponding Git tag `2025-05-01-preview` or download it directly from the [release page](https://github.com/Azure-Samples/azure-ai-content-understanding-dotnet/releases/tag/2025-05-01-preview).

👉 If you are looking for **Python samples**, check out [this repo](https://github.com/Azure-Samples/azure-ai-content-understanding-python/).

---

# Quick Start

You can run this sample in **GitHub Codespaces** or on your **local machine**.

---

## Option 1: GitHub Codespaces

Run this repo virtually in a cloud-based development environment.

[![Open in GitHub Codespaces](https://github.com/codespaces/badge.svg)](https://github.com/codespaces/new?skip_quickstart=true&machine=basicLinux32gb&repo=1012664126&ref=main&geo=UsEast&devcontainer_path=.devcontainer%2Fdevcontainer.json)

### Steps:

1. Click the button above to create a new Codespace.
2. Select the `main` branch, your preferred region, and a 2-core machine.
3. When the Codespace is ready, VS Code will automatically build the Dev Container.
4. Follow the instructions in [Configure Azure AI service resource](#configure-azure-ai-service-resource).
5. Use the integrated terminal to run the project:

```bash
cd {ProjectName}
dotnet build
cd bin/Debug/net8.0/
dotnet {ProjectName}.dll
```

6. For an overview of the available projects in {ProjectName}, refer to the [Features](#features) section.
   
   **⚠️ IMPORTANT:** If you plan to use prebuilt analyzers (like in **ContentExtraction**), you must first run the **ModelDeploymentSetup** sample. See [Step 4: Configure Model Deployments](#step-4-configure-model-deployments-required-for-prebuilt-analyzers) for details.

---

## Option 2: Run Locally

To run the project locally, select one of the setup options below.  
For the smoothest experience, we recommend Option 2.1, which provides a hassle-free environment setup.

---

### Option 2.1: Windows / macOS / Linux (using VS Code + Dev Container)

#### Prerequisites

- **Docker**  
  Install [Docker Desktop](https://www.docker.com/products/docker-desktop/) (available for Windows, macOS, and Linux).  
  Docker is used to manage and run the container environment.  
  - Start Docker and ensure it is running in the background.

- **Visual Studio Code**  
  Download and install [Visual Studio Code](https://code.visualstudio.com/).

- **Dev Containers Extension**  
  In the VS Code extension marketplace, install the extension named **Dev Containers**.  
  (This extension was previously called "Remote - Containers" but has since been renamed and integrated into **Dev Containers**.)

#### Steps

1. Clone the repo:

```bash
git clone https://github.com/Azure-Samples/azure-ai-content-understanding-dotnet.git
cd azure-ai-content-understanding-dotnet
```

2. Launch VS Code and open the folder:

```bash
code .
```

3. Press `F1`, then select `Dev Containers: Reopen in Container`.

4. Wait for the setup to complete. Follow the instructions in [Configure Azure AI service resource](#configure-azure-ai-service-resource), then use the integrated terminal in Visual Studio Code to run:

```bash
cd {ProjectName}
dotnet build
cd bin/Debug/net8.0/
dotnet {ProjectName}.dll
```

5. For an overview of the available projects in {ProjectName}, refer to the [Features](#features) section. 
   
   **⚠️ IMPORTANT:** If you plan to use prebuilt analyzers (like in **ContentExtraction**), you must first run the **ModelDeploymentSetup** sample. See [Step 4: Configure Model Deployments](#step-4-configure-model-deployments-required-for-prebuilt-analyzers) for details.

---

### Option 2.2: Windows (using Visual Studio 2022 or later)

#### Prerequisites

- [.NET SDK 8.0+](https://dotnet.microsoft.com/en-us/download)
- [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli)
- [Azure Developer CLI (azd)](https://aka.ms/install-azd)
- [Git LFS](https://git-lfs.com/)

1. Clone the repo using Git or from Visual Studio:

```bash
git clone https://github.com/Azure-Samples/azure-ai-content-understanding-dotnet
```

2. Open the `.sln` solution file in **[Visual Studio 2022+](https://visualstudio.microsoft.com/downloads/)**.

3. Ensure the target framework is set to **.NET 8**.

4. Follow the instructions in [Configure Azure AI service resource](#configure-azure-ai-service-resource).

5. For an overview of the available projects, refer to the [Features](#features) section.
   
   **⚠️ IMPORTANT:** If you plan to use prebuilt analyzers (like in **ContentExtraction**), you must first run the **ModelDeploymentSetup** sample. See [Step 4: Configure Model Deployments](#step-4-configure-model-deployments-required-for-prebuilt-analyzers) for details.

6. Press **F5** or click **Start** to run the console app.

---

## <a name="configure-azure-ai-service-resource">Configure Azure AI Service Resource</a>

### Step 1: Create Azure AI Foundry Resource

First, create an Azure AI Foundry resource that will host both the Content Understanding service and the required model deployments.

1. Follow the steps in the [Azure Content Understanding documentation](https://learn.microsoft.com/en-us/azure/ai-services/content-understanding/) to create an Azure AI Foundry resource
2. Get your Foundry resource's endpoint URL from Azure Portal:
   - Go to [Azure Portal](https://portal.azure.com/)
   - Navigate to your Azure AI Foundry resource
   - Go to **Resource Management** > **Keys and Endpoint**
   - Copy the **Endpoint** URL (typically `https://<your-resource-name>.services.ai.azure.com/`)

**⚠️ Important: Grant Required Permissions**

After creating your Azure AI Foundry resource, you must grant yourself the **Cognitive Services User** role to enable API calls for setting default GPT deployments:

1. Go to [Azure Portal](https://portal.azure.com/)
2. Navigate to your Azure AI Foundry resource
3. Go to **Access Control (IAM)** in the left menu
4. Click **Add** > **Add role assignment**
5. Select the **Cognitive Services User** role
6. Assign it to yourself (or the user/service principal that will run the samples)

> **Note:** This role assignment is required even if you are the owner of the resource. Without this role, you will not be able to call the Content Understanding API to configure model deployments for prebuilt analyzers.

### Step 2: Deploy Required Models

**⚠️ Important:** The prebuilt analyzers require model deployments. You must deploy these models before using prebuilt analyzers:
- `prebuilt-documentSearch`, `prebuilt-audioSearch`, `prebuilt-videoSearch` require **GPT-4.1-mini** and **text-embedding-3-large**
- Other prebuilt analyzers like `prebuilt-invoice`, `prebuilt-receipt` require **GPT-4.1** and **text-embedding-3-large**

1. **Deploy GPT-4.1:**
   - In Azure AI Foundry, go to **Deployments** > **Deploy model** > **Deploy base model**
   - Search for and select **gpt-4.1**
   - Complete the deployment with your preferred settings
   - Note the deployment name (by convention, use `gpt-4.1`)

2. **Deploy GPT-4.1-mini:**
   - In Azure AI Foundry, go to **Deployments** > **Deploy model** > **Deploy base model**
   - Search for and select **gpt-4.1-mini**
   - Complete the deployment with your preferred settings
   - Note the deployment name (by convention, use `gpt-4.1-mini`)

3. **Deploy text-embedding-3-large:**
   - In Azure AI Foundry, go to **Deployments** > **Deploy model** > **Deploy base model**
   - Search for and select **text-embedding-3-large**
   - Complete the deployment with your preferred settings
   - Note the deployment name (by convention, use `text-embedding-3-large`)

For more information on deploying models, see [Deploy models in Azure AI Foundry](https://learn.microsoft.com/en-us/azure/ai-studio/how-to/deploy-models-openai).

### Step 3: Configure appsettings.json

Choose one of the following options to configure your application:

#### Option A: Use Token Authentication (Recommended)

> **Recommended:** This approach uses Azure Active Directory (AAD) token authentication, which is safer and strongly recommended for production environments. You do **not** need to set `AZURE_AI_API_KEY` in your `appsettings.json` file when using this method.

1. Copy the sample appsettings file:

   ```bash
   cp ContentUnderstanding.Common/appsettings.example.json ContentUnderstanding.Common/appsettings.json
   ```

2. Open `ContentUnderstanding.Common/appsettings.json` and fill in the required values. Replace `<your-resource-name>` with your actual resource name. If you used different deployment names in Step 2, update the deployment variables accordingly:

   ```json
   {
     "AZURE_AI_ENDPOINT": "https://<your-resource-name>.services.ai.azure.com",
     "AZURE_AI_API_KEY": null,
     "AZURE_AI_API_VERSION": "2025-11-01",
     "GPT_4_1_DEPLOYMENT": "gpt-4.1",
     "GPT_4_1_MINI_DEPLOYMENT": "gpt-4.1-mini",
     "TEXT_EMBEDDING_3_LARGE_DEPLOYMENT": "text-embedding-3-large",
     "TRAINING_DATA_SAS_URL": null,
     "TRAINING_DATA_PATH": null
   }
   ```
   
   > **Note:** See the [appsettings.json Configuration Reference](#appsettingsjson-configuration-reference) section below for detailed explanations of each setting, since JSON files cannot contain comments.

3. Log in to Azure:

   ```bash
   azd auth login
   ```

   If this does not work, try:

   ```bash
   azd auth login --use-device-code
   ```

   and follow the on-screen instructions.

#### Option B: Use API Key (Alternative)

1. Copy the sample appsettings file:

   ```bash
   cp ContentUnderstanding.Common/appsettings.example.json ContentUnderstanding.Common/appsettings.json
   ```

2. Edit `ContentUnderstanding.Common/appsettings.json` and set your credentials:
   - Replace `<your-resource-name>` and `<your-azure-ai-api-key>` with your actual values. These can be found in your AI Services resource under **Resource Management** > **Keys and Endpoint**.
   - If you used different deployment names in Step 2, update the deployment variables accordingly:

   ```json
   {
     "AZURE_AI_ENDPOINT": "https://<your-resource-name>.services.ai.azure.com",
     "AZURE_AI_API_KEY": "<your-azure-ai-api-key>",
     "AZURE_AI_API_VERSION": "2025-11-01",
     "GPT_4_1_DEPLOYMENT": "gpt-4.1",
     "GPT_4_1_MINI_DEPLOYMENT": "gpt-4.1-mini",
     "TEXT_EMBEDDING_3_LARGE_DEPLOYMENT": "text-embedding-3-large",
     "TRAINING_DATA_SAS_URL": null,
     "TRAINING_DATA_PATH": null
   }
   ```
   
   > **Note:** See the [appsettings.json Configuration Reference](#appsettingsjson-configuration-reference) section below for detailed explanations of each setting, since JSON files cannot contain comments.

> ⚠️ **Note:** If you skip the token authentication step above, you must set `AZURE_AI_API_KEY` in your `appsettings.json` file. Get your API key from Azure Portal by navigating to your Foundry resource > **Resource Management** > **Keys and Endpoint**.

### (Alternative) Use `azd` commands to automatically create temporary resources

1. Ensure you have permission to grant roles under your subscription.

2. Login to Azure:

```shell
azd auth login
```

If this does not work, try:

```shell
azd auth login --use-device-code
```

and follow the on-screen instructions.

3. Set up the environment, following prompts to choose the location:

```shell
azd up
```

---

## appsettings.json Configuration Reference

> **Note:** Unlike `.env` files which support comments, JSON files cannot contain comments. All configuration explanations are provided in this section.

After copying `appsettings.example.json` to `appsettings.json`, configure the following settings:

### Required Settings

- **`AZURE_AI_ENDPOINT`** (Required)
  - Your Azure AI Foundry resource endpoint URL
  - Format: `https://<your-resource-name>.services.ai.azure.com`
  - Get this from Azure Portal: Your Foundry resource > **Resource Management** > **Keys and Endpoint**

- **`GPT_4_1_DEPLOYMENT`** (Required for prebuilt analyzers like `prebuilt-invoice`, `prebuilt-receipt`)
  - The deployment name for GPT-4.1 model in your Azure AI Foundry resource
  - Default: `gpt-4.1` (if you used this name during deployment)
  - Required along with `TEXT_EMBEDDING_3_LARGE_DEPLOYMENT` for certain prebuilt analyzers

- **`GPT_4_1_MINI_DEPLOYMENT`** (Required for prebuilt analyzers like `prebuilt-documentSearch`, `prebuilt-audioSearch`, `prebuilt-videoSearch`)
  - The deployment name for GPT-4.1-mini model in your Azure AI Foundry resource
  - Default: `gpt-4.1-mini` (if you used this name during deployment)
  - Required along with `TEXT_EMBEDDING_3_LARGE_DEPLOYMENT` for search-related prebuilt analyzers

- **`TEXT_EMBEDDING_3_LARGE_DEPLOYMENT`** (Required for prebuilt analyzers)
  - The deployment name for text-embedding-3-large model in your Azure AI Foundry resource
  - Default: `text-embedding-3-large` (if you used this name during deployment)
  - Required for all prebuilt analyzers that use embeddings

### Optional Settings

- **`AZURE_AI_API_KEY`** (Optional)
  - Your Azure AI Foundry API key for key-based authentication
  - **WARNING:** Keys are less secure and should only be used for testing/development
  - Leave as `null` to use DefaultAzureCredential (recommended for production)
  - Get this from Azure Portal: Your Foundry resource > **Resource Management** > **Keys and Endpoint**
  - If using DefaultAzureCredential, ensure you're logged in with `azd auth login` or `az login`

- **`AZURE_AI_API_VERSION`** (Optional)
  - The API version to use for Content Understanding
  - Default: `2025-11-01` (GA version)
  - Only change if you need to use a different API version

- **`TRAINING_DATA_SAS_URL`** (Optional - Only required for `AnalyzerTraining` sample)
  - SAS URL for the Azure Blob container containing training data
  - Format: `https://<storage-account-name>.blob.core.windows.net/<container-name>?<sas-token>`
  - Only needed when running the analyzer training sample
  - **Note:** Currently, the `AnalyzerTraining` sample prompts for this value interactively at runtime. You can set it in `appsettings.json` for convenience, but the sample will still prompt if not provided via configuration.
  - For more information, see [Set up training data](docs/set_env_for_training_data_and_reference_doc.md)

- **`TRAINING_DATA_PATH`** (Optional - Only required for `AnalyzerTraining` sample)
  - Folder path within the blob container where training data is stored
  - Example: `training_data/` or `labeling-data/`
  - Only needed when running the analyzer training sample
  - **Note:** Currently, the `AnalyzerTraining` sample prompts for this value interactively at runtime. You can set it in `appsettings.json` for convenience, but the sample will still prompt if not provided via configuration.
  - For more information, see [Set up training data](docs/set_env_for_training_data_and_reference_doc.md)

### Authentication Methods

**Option 1: DefaultAzureCredential (Recommended)**
- Set `AZURE_AI_API_KEY` to `null`
- Most common development scenario:
  1. Install [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli)
  2. Login: `az login` or `azd auth login`
  3. Run the application (no additional configuration needed)
- Also supports:
  - Environment variables (`AZURE_CLIENT_ID`, `AZURE_CLIENT_SECRET`, `AZURE_TENANT_ID`)
  - Managed Identity (for Azure-hosted applications)
  - Visual Studio Code authentication
  - Azure PowerShell authentication
- For more info: [DefaultAzureCredential documentation](https://learn.microsoft.com/en-us/dotnet/api/azure.identity.defaultazurecredential)

**Option 2: API Key (For Testing Only)**
- Set `AZURE_AI_API_KEY` to your API key value
- Less secure - only recommended for local testing/development
- Get your API key from Azure Portal: Your Foundry resource > **Resource Management** > **Keys and Endpoint**

---

## Step 4: Configure Model Deployments (Required for Prebuilt Analyzers)

> ⚠️ **IMPORTANT:** Before running any samples that use prebuilt analyzers (like `ContentExtraction`), you must configure the model deployments. This is a **one-time setup** that maps your deployed models to the prebuilt analyzers.

1. **Run the ModelDeploymentSetup sample:**

   ```bash
   cd ModelDeploymentSetup
   dotnet build
   dotnet run
   ```

2. This sample will:
   - Read your deployment names from `appsettings.json`
   - Configure the default model mappings in your Azure AI Foundry resource
   - Verify that all required deployments are configured correctly

3. **After successful configuration**, you can run other samples that use prebuilt analyzers.

> **Note:** The configuration is persisted in your Azure AI Foundry resource, so you only need to run this once (or whenever you change your deployment names).

---

## Features

Azure AI Content Understanding is a new Generative AI-based [Azure AI service](https://learn.microsoft.com/en-us/azure/ai-services/content-understanding/overview) designed to process and ingest content of any type—documents, images, audio, and video—into a user-defined output format. Content Understanding provides a streamlined way to analyze large volumes of unstructured data, accelerating time-to-value by generating output that can be integrated into automation and analytical workflows.

> **Documentation:** Each sample includes a detailed `README.md` file with concepts, code examples, and implementation details. See the sample directories for comprehensive documentation.

| Project                     | Key Source File                   | Description |
|-----------------------------|----------------------------------|-------------|
| **[ModelDeploymentSetup](ModelDeploymentSetup/)** ⚠️ **Run First** | [Program.cs](ModelDeploymentSetup/Program.cs) | **REQUIRED:** This sample configures the default model deployments required for prebuilt analyzers. You must run this once before running any other samples that use prebuilt analyzers (like ContentExtraction). |
| [ContentExtraction](ContentExtraction/) | [ContentExtractionService.cs](ContentExtraction/Services/ContentExtractionService.cs) | In this sample we will show content understanding API can help you get semantic information from your file. For example OCR with table in document, audio transcription, and face analysis in video. |
| [FieldExtraction](FieldExtraction/)   | [FieldExtractionService.cs](FieldExtraction/Services/FieldExtractionService.cs) | In this sample we will show how to create an analyzer to extract fields in your file. For example invoice amount in the document, how many people in an image, names mentioned in an audio, or summary of a video. You can customize the fields by creating your own analyzer template. |
| [Classifier](Classifier/) | [ClassifierService.cs](Classifier/Services/ClassifierService.cs) | This sample will demo how to (1) create a classifier to categorize documents, (2) create a custom analyzer to extract specific fields, and (3) combine classifier and analyzers to classify, optionally split, and analyze documents in a flexible processing pipeline. |
| [ConversationalFieldExtraction](ConversationalFieldExtraction/) | [ConversationalFieldExtraction.cs](ConversationalFieldExtraction/Services/ConversationalFieldExtractionService.cs) | Shows how to efficiently evaluate conversational audio data previously transcribed with Content Understanding or Azure AI Speech. Enables re-analysis of data cost-effectively. Based on the [FieldExtraction](FieldExtraction/) sample. 
| [AnalyzerTraining](AnalyzerTraining/) | [AnalyzerTrainingService.cs](AnalyzerTraining/Services/AnalyzerTrainingService.cs) | If you want to futher boost the performance for field extraction, we can do training when you provide few labeled samples to the API. Note: This feature is available to document scenario now. |
| [Management](Management/)      | [ManagementService.cs](Management/Services/ManagementService.cs) | This sample will demo how to create a minimal analyzer, list all the analyzers in your resource, and delete the analyzer you don't need. |
| [BuildPersonDirectory](BuildPersonDirectory/)      | [BuildPersonDirectoryService.cs](BuildPersonDirectory/Services/BuildPersonDirectoryService.cs) | This sample will demo how to enroll people’s faces from images and build a Person Directory. |

---

## Sample Console Output

Here is an example of the console output from the **ContentExtraction** project.

```
$ dotnet ContentExtraction.dll
Please enter a number to run sample: 
[1] - Extract Document Content
[2] - Extract Audio Content
[3] - Extract Video Content
[4] - Extract Video Content With Face 
1
Document Content Extraction Sample is running...
Use prebuilt-documentAnalyzer to extract document content from the file: ./data/invoice.pdf

===== Document Extraction has been saved to the following output file path =====

./outputs/content_extraction/AnalyzeDocumentAsync_20250714034618.json

===== The markdown output contains layout information, which is very useful for Retrieval-Augmented Generation (RAG) scenarios. You can paste the markdown into a viewer such as Visual Studio Code and preview the layout structure. =====
CONTOSO LTD.


# INVOICE

Contoso Headquarters
123 456th St
New York, NY, 10001

INVOICE: INV-100

INVOICE DATE: 11/15/2019

DUE DATE: 12/15/2019

CUSTOMER NAME: MICROSOFT CORPORATION

SERVICE PERIOD: 10/14/2019 - 11/14/2019

CUSTOMER ID: CID-12345

<<< Truncated for brevity >>>
```

---

## More Samples Using Azure Content Understanding

> **Note:** The following samples are currently targeting Preview.2 (API version `2025-05-01-preview`) and will be updated to the GA API version (`2025-11-01`) soon.

- [Azure Content Understanding Samples (Python)](https://github.com/Azure-Samples/azure-ai-content-understanding-python)
- [Azure Search with Content Understanding](https://github.com/Azure-Samples/azure-ai-search-with-content-understanding-python)
- [Azure Content Understanding with OpenAI](https://github.com/Azure-Samples/azure-ai-content-understanding-with-azure-openai-python)

---

## Additional Resources

- [Azure Content Understanding Documentation](https://learn.microsoft.com/en-us/azure/ai-services/content-understanding/overview)
- [Region and Language Support](https://learn.microsoft.com/en-us/azure/ai-services/content-understanding/language-region-support)

---

## Notes

* **Trademarks** - This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft trademarks or logos is subject to and must follow [Microsoft's Trademark & Brand Guidelines](https://www.microsoft.com/en-us/legal/intellectualproperty/trademarks/usage/general). Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship. Any use of third-party trademarks or logos is subject to those third-party’s policies.

* **Data Collection** - The software may collect information about you and your use of the software and send it to Microsoft. Microsoft may use this information to provide services and improve our products and services. You may turn off the telemetry as described in the repository. There are also some features in the software that may enable you and Microsoft to collect data from users of your applications. If you use these features, you must comply with applicable law, including providing appropriate notices to users of your applications together with a copy of Microsoft’s privacy statement. Our privacy statement is located at https://go.microsoft.com/fwlink/?LinkID=824704. You can learn more about data collection and use in the help documentation and our privacy statement. Your use of the software operates as your consent to these practices.