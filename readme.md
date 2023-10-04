# GPTivity

This code is for educational purposes, not intended for production usage.

_GPTivity is available under the MIT License. See the LICENSE file for more info._

## Installation

The application used in the demo is named _*NSerio Document Abstract Chat*_

** ___after install, wait 5 - 10 minutes to have the option available in the document viewer___

### RAP
The rap used for the demo in Relativity Fest 2023 can be found in `\Relativity Application\NDAC.rap`

### Instance Settings
The application requires to create 2 instance settings, we recommend to use Encrypted Instance Settings:

| Section | Name | Description |
| ------- | ---- | ----------- |
| ndac    | url  | The url of a Chat Playground deployment of Azure OpenAI |
| ndac    | key  | The key of a Chat Playground deployment of Azure OpenAI |

### Application Resource Files & Code

Here's the relationship between the repository code and each Resource File

#### NSerio.DocumentAbstractChat.dll
The result of compiling of the project `\NSerio.Relativity.DocumentAbstractChat\NSerio.DocumentAbstractChat.csproj`

Contains the Kepler service `DocumentAbstractChatManager`

##### DocumentAbstractChatManager

```
BASE_ROUTE: /Relativity.REST/api/nserio/dac

// Get the abstract of a document stored in NDAC Abstract field
GET     /{workspaceID:int}/{documentArtifactID:int}/abstract
          HTTPCODE 200: Content has the abstract
          HTTPCODE 412: Document has no extracted text or abstract set

// Generate the abstract of a document and store it in NDAC Abstract field
POST    /{workspaceID:int}/{documentArtifactID:int}/abstract
          HTTPCODE 200: Content has the abstract
          HTTPCODE 412: Document has no extracted text or abstract set

/// Get the chat history of a document
GET     /{workspaceID:int}/{documentArtifactID:int}/chat
          HTTPCODE 200: Content has a JSON with an array of Question/Answer object

/// Ask questions to the extracted text of a document
POST    /{workspaceID:int}/{documentArtifactID:int}/chat
        { 'question': 'prompt to send to OpenAI' }
          HTTPCODE 200: Content has the answer
          HTTPCODE 412: Document has no extracted text
```

#### review.index.documentAbstractChat.js
Code in `\NSerio.Relativity.DocumentAbstractChat\review.index.documentAbstractChat.js` for more information: https://platform.relativity.com/RelativityOne/Content/Core_reviewer_interface/Relativity_Review_API.htm

#### review.documentAbstractChat.html
Code in `\NSerio.Relativity.DocumentAbstractChat\review.documentAbstractChat.html`

#### review.documentAbstractChat.png
Image in `\NSerio.Relativity.DocumentAbstractChat\review.documentAbstractChat.png`

