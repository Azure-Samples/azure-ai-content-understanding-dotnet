# Set env variables for training data and reference doc for Pro mode
Folders [document_training](../data/document_training/) and [field_extraction_pro_mode](../data/field_extraction_pro_mode) contain the manually labeled data for training and reference doc for Pro mode as a quick sample. Before using these knowledge source files, you need an Azure Storage blob container to store them. Let's follow below steps to prepare the data environment:

1. *Create an Azure Storage Account:* If you don’t already have one, follow the guide to [create an Azure Storage Account](https://aka.ms/create-a-storage-account).
    > If you already have an account, you can skip this step.
2. *Install Azure Storage Explorer:* Azure Storage Explorer is a tool which makes it easy to work with Azure Storage data. Install it and login with your credential, follow the [guide](https://aka.ms/download-and-install-Azure-Storage-Explorer).
3. *Create or Choose a Blob Container:* Create a blob container from Azure Storage Explorer or use an existing one.  
   <img src="./create-blob-container.png" width="600" />  
4. *Generate a Shared Access Signature (SAS) URL:*
    - Right-click on blob container and select the `Get Shared Access Signature...` in the menu.
    - Check the required permissions: `Read`, `Write` and `List`
    - Click the `Create` button.  
   <img src="./get-access-signature.png" height="600" />  <img src="./choose-signature-options.png" height="600" />  
5. *Copy the SAS URL:* After creating the SAS, click `Copy` to get the URL with token. This will be used as the value for **TRAINING_DATA_SAS_URL** or **REFERENCE_DOC_SAS_URL** when running the sample code.  
   <img src="./copy-access-signature.png" width="600" />  
6. *Set Environment Variables in ".env" File:* Depending on the sample that you will run, you will need to set required environment variables in [.env](../notebooks/.env).
    > NOTE: **REFERENCE_DOC_SAS_URL** can be the same as the **TRAINING_DATA_SAS_URL** to re-use the same blob container
    - [analyzer_training](../notebooks/analyzer_training.ipynb): Add the SAS URL as value of **TRAINIGN_DATA_SAS_URL**, and a prefix for **TRAINING_DATA_PATH**. You can choose any folder name you like for **TRAINING_DATA_PATH**. For example, you could use "training_files".
        ```env
        TRAINING_DATA_SAS_URL=<Blob container SAS URL>
        TRAINING_DATA_PATH=<Designated folder path under the blob container>
        ```
    - [field_extraction_pro_mode](../notebooks/field_extraction_pro_mode.ipynb): Add the SAS URL as value of **REFERENCE_DOC_SAS_URL**, and a prefix for **REFERENCE_DOC_PATH**. You can choose any folder name you like for **REFERENCE_DOC_PATH**. For example, you could use "reference_docs".
        ```env
        REFERENCE_DOC_SAS_URL=<Blob container SAS URL>
        REFERENCE_DOC_PATH=<Designated folder path under the blob container>
        ```

Now, we have completed the preparation of the data environment. Next, we could create an analyzer through code.

   
