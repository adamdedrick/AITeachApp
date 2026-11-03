namespace AITeach1;

public sealed class ChatForm : Form
{
    private const string DefaultModel = "llama3.2:3b";
    private readonly string _folderPath;
    private readonly Services.RagChatService _chatService = new();
    private readonly RichTextBox _conversation = new();
    private readonly TextBox _documentsList = new();
    private readonly TextBox _questionInput = new();
    private readonly TextBox _modelInput = new();
    private readonly Button _sendButton = new();
    private readonly Label _statusLabel = new();

    public ChatForm(string folderPath)
    {
        _folderPath = Path.GetFullPath(folderPath);
        Text = "AITeach1 | Ask a question";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(960, 720);
        MinimumSize = new Size(780, 600);
        BackColor = Color.FromArgb(243, 246, 251);
        Font = new Font("Segoe UI", 10F);

        var heading = new Label
        {
            AutoSize = false,
            Location = new Point(28, 20),
            Size = new Size(650, 38),
            Text = "Ask your documents",
            Font = new Font("Segoe UI", 21F, FontStyle.Bold),
            ForeColor = Color.FromArgb(20, 33, 61)
        };

        var folderLabel = new Label
        {
            AutoSize = false,
            Location = new Point(32, 62),
            Size = new Size(670, 23),
            Text = $"Indexed folder: {_folderPath}",
            ForeColor = Color.FromArgb(91, 103, 125),
            Font = new Font("Segoe UI", 9F),
            AutoEllipsis = true
        };

        var modelLabel = new Label
        {
            AutoSize = true,
            Location = new Point(720, 31),
            Text = "Ollama model",
            ForeColor = Color.FromArgb(91, 103, 125),
            Font = new Font("Segoe UI", 9F)
        };

        _modelInput.Location = new Point(720, 56);
        _modelInput.Size = new Size(208, 27);
        _modelInput.Text = DefaultModel;

        var documentsLabel = new Label
        {
            AutoSize = true,
            Location = new Point(32, 101),
            Text = "DOCUMENTS IN THIS INDEX",
            ForeColor = Color.FromArgb(20, 33, 61),
            Font = new Font("Segoe UI Semibold", 9F)
        };

        _documentsList.Location = new Point(28, 124);
        _documentsList.Size = new Size(900, 92);
        _documentsList.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _documentsList.Multiline = true;
        _documentsList.ReadOnly = true;
        _documentsList.WordWrap = true;
        _documentsList.ScrollBars = ScrollBars.Vertical;
        _documentsList.BackColor = Color.White;
        _documentsList.ForeColor = Color.FromArgb(39, 50, 72);
        _documentsList.BorderStyle = BorderStyle.FixedSingle;
        _documentsList.Font = new Font("Segoe UI", 9F);
        _documentsList.Text = "Loading indexed documents…";

        _conversation.Location = new Point(28, 228);
        _conversation.Size = new Size(900, 346);
        _conversation.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        _conversation.ReadOnly = true;
        _conversation.BackColor = Color.White;
        _conversation.ForeColor = Color.FromArgb(39, 50, 72);
        _conversation.BorderStyle = BorderStyle.FixedSingle;
        _conversation.Font = new Font("Segoe UI", 10F);
        _conversation.DetectUrls = false;
        _conversation.Text = "Ask a question about the documents in this index. The app retrieves relevant passages from ChromaDB and sends them to your local Ollama model.\n";

        _statusLabel.Location = new Point(32, 584);
        _statusLabel.Size = new Size(890, 22);
        _statusLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        _statusLabel.ForeColor = Color.FromArgb(91, 103, 125);
        _statusLabel.Font = new Font("Segoe UI", 9F);
        _statusLabel.Text = "Ready. Chat model: llama3.2:3b. Embeddings: nomic-embed-text.";

        _questionInput.Location = new Point(28, 612);
        _questionInput.Size = new Size(770, 64);
        _questionInput.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        _questionInput.Multiline = true;
        _questionInput.AcceptsReturn = true;
        _questionInput.ScrollBars = ScrollBars.Vertical;
        _questionInput.Font = new Font("Segoe UI", 10F);
        _questionInput.KeyDown += QuestionInput_KeyDown;

        _sendButton.Location = new Point(812, 612);
        _sendButton.Size = new Size(116, 64);
        _sendButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        _sendButton.Text = "Send";
        _sendButton.BackColor = Color.FromArgb(14, 165, 133);
        _sendButton.ForeColor = Color.White;
        _sendButton.FlatStyle = FlatStyle.Flat;
        _sendButton.FlatAppearance.BorderSize = 0;
        _sendButton.Font = new Font("Segoe UI Semibold", 10F);
        _sendButton.UseVisualStyleBackColor = false;
        _sendButton.Cursor = Cursors.Hand;
        _sendButton.Click += async (_, _) => await SendQuestionAsync();

        Controls.AddRange([heading, folderLabel, modelLabel, _modelInput, documentsLabel, _documentsList, _conversation, _statusLabel, _questionInput, _sendButton]);
        Shown += async (_, _) => await LoadIndexedDocumentsAsync();
        FormClosed += (_, _) => _chatService.Dispose();
    }

    private async Task LoadIndexedDocumentsAsync()
    {
        try
        {
            var documents = await _chatService.GetIndexedDocumentsAsync(
                Path.Combine(_folderPath, "chroma_db"), _folderPath);
            if (!IsDisposed)
            {
                _documentsList.Text = documents.Count == 0
                    ? "No indexed documents were found for this folder."
                    : string.Join(Environment.NewLine, documents);
            }
        }
        catch (Exception exception)
        {
            if (!IsDisposed)
            {
                _documentsList.Text = $"Could not load indexed documents: {exception.Message}";
            }
        }
    }

    private async Task SendQuestionAsync()
    {
        var question = _questionInput.Text.Trim();
        var model = _modelInput.Text.Trim();
        if (question.Length == 0)
        {
            return;
        }
        if (model.Length == 0)
        {
            MessageBox.Show(this, "Enter the name of an Ollama chat model.", "Model required", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _questionInput.Clear();
        _questionInput.Enabled = false;
        _sendButton.Enabled = false;
        _modelInput.Enabled = false;
        AppendMessage("You", question);
        _statusLabel.Text = "Searching the index and generating an answer…";
        try
        {
            var answer = await _chatService.AskAsync(
                Path.Combine(_folderPath, "chroma_db"), model, question);
            AppendMessage("Assistant", answer);
            _statusLabel.Text = $"Answered using {model} and the selected ChromaDB index.";
        }
        catch (Exception exception)
        {
            AppendMessage("Error", exception.Message);
            _statusLabel.Text = "Could not complete the request. See the conversation for details.";
        }
        finally
        {
            _questionInput.Enabled = true;
            _sendButton.Enabled = true;
            _modelInput.Enabled = true;
            _questionInput.Focus();
        }
    }

    private void AppendMessage(string speaker, string message)
    {
        _conversation.AppendText($"\n{speaker}\n{message}\n");
        _conversation.SelectionStart = _conversation.TextLength;
        _conversation.ScrollToCaret();
    }

    private async void QuestionInput_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter && !e.Shift)
        {
            e.SuppressKeyPress = true;
            await SendQuestionAsync();
        }
    }
}