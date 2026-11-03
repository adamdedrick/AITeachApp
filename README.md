# AITeach1 — To help in learning new things.

Place all the documentation you have for a new project in a folder.
Run AITeach.
Select RAGIT and select the folder and it will build a RAG index of all the documents.
Select RAGChat and converse with an LLM about the documents supplied.
Simples.



## Requirements

- Windows and the .NET 9 SDK.
- Ollama running locally, with the `nomic-embed-text` model available.
- Python installed, with ChromaDB installed so the `chroma` command is on `PATH`.

One-time setup in PowerShell:

```powershell
ollama pull nomic-embed-text
ollama pull llama3.2:3b
py -m pip install chromadb
```

Start Ollama before indexing. The app launches a local ChromaDB server itself, configured to store its database in the selected folder. It shuts that server down when the app closes; the database remains on disk.

## Run

Open `AITeach1.csproj` in Visual Studio and press **F5**, or run from the project folder:

```powershell
dotnet run
```

Select **Create a ChromaDB index** and choose a folder. Indexing includes supported files in nested folders. Rebuilding a folder replaces its existing entries. To chat with an index, select **Ask a question**, choose the same folder, enter a question, and send it. The app retrieves relevant passages from ChromaDB and provides them as context to the local Ollama chat model. The chat model defaults to `llama3.2:3b` and can be changed in the chat window. Scanned text stays on the machine and is sent only to the local Ollama service.

## Build

```powershell
dotnet build
```

PDF text extraction uses PdfPig; legacy `.doc` and `.docx` extraction use NPOI. Tables in `.docx` files are included. Image-only/scanned PDFs without embedded text are not OCR-processed.