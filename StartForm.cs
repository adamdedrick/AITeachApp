namespace AITeach1;

public sealed class StartForm : Form
{
    public StartForm()
    {
        Text = "AITeach1 | Start";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(820, 560);
        MinimumSize = new Size(760, 520);
        BackColor = Color.FromArgb(243, 246, 251);
        Font = new Font("Segoe UI", 10F);

        var heading = new Label
        {
            AutoSize = false,
            Location = new Point(52, 46),
            Size = new Size(700, 48),
            Text = "What would you like to do?",
            Font = new Font("Segoe UI", 25F, FontStyle.Bold),
            ForeColor = Color.FromArgb(20, 33, 61)
        };

        var subtitle = new Label
        {
            AutoSize = false,
            Location = new Point(56, 102),
            Size = new Size(700, 28),
            Text = "Choose a task to get started.",
            Font = new Font("Segoe UI", 10.5F),
            ForeColor = Color.FromArgb(91, 103, 125)
        };

        var ragItCard = CreateOptionCard(
            "Prepare RAG Index",
            "Read documents from a folder and build a local searchable RAG index.",
            "RAGIT",
            Color.FromArgb(37, 99, 235),
            OpenIndexer);

        var ragChatCard = CreateOptionCard(
            "Chat with your RAG index",
            "Search an indexed folder and chat with a local Ollama model.",
            "RAGCHAT",
            Color.FromArgb(14, 165, 133),
            OpenChat);

        ragItCard.Location = new Point(56, 168);
        ragChatCard.Location = new Point(56, 338);

        Controls.AddRange([heading, subtitle, ragItCard, ragChatCard]);
    }

    private static Panel CreateOptionCard(
        string title,
        string description,
        string buttonText,
        Color accentColor,
        Action? onClick)
    {
        var card = new Panel
        {
            Size = new Size(708, 146),
            BackColor = Color.White
        };

        var accent = new Panel
        {
            Location = new Point(0, 0),
            Size = new Size(6, card.Height),
            BackColor = accentColor
        };

        var titleLabel = new Label
        {
            AutoSize = false,
            Location = new Point(28, 24),
            Size = new Size(420, 31),
            Text = title,
            Font = new Font("Segoe UI Semibold", 15F),
            ForeColor = Color.FromArgb(20, 33, 61)
        };

        var descriptionLabel = new Label
        {
            AutoSize = false,
            Location = new Point(29, 62),
            Size = new Size(430, 44),
            Text = description,
            Font = new Font("Segoe UI", 9.5F),
            ForeColor = Color.FromArgb(91, 103, 125)
        };

        var optionButton = new Button
        {
            Location = new Point(492, 48),
            Size = new Size(184, 48),
            Text = buttonText,
            BackColor = accentColor,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Semibold", 9F),
            UseVisualStyleBackColor = false,
            Cursor = onClick is null ? Cursors.Default : Cursors.Hand,
            Enabled = onClick is not null
        };
        optionButton.FlatAppearance.BorderSize = 0;
        if (onClick is not null)
        {
            optionButton.Click += (_, _) => onClick();
        }

        card.Controls.AddRange([accent, titleLabel, descriptionLabel, optionButton]);
        return card;
    }

    private void OpenIndexer()
    {
        Hide();
        var indexer = new IndexForm();
        indexer.FormClosed += (_, _) =>
        {
            if (!IsDisposed)
            {
                Show();
                Activate();
            }
        };
        indexer.Show(this);
    }

    private void OpenChat()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select the folder that contains the documents you indexed (with its chroma_db subfolder).",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };

        if (dialog.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(dialog.SelectedPath))
        {
            return;
        }

        var databasePath = Path.Combine(dialog.SelectedPath, "chroma_db");
        if (!Directory.Exists(databasePath))
        {
            MessageBox.Show(
                this,
                "This folder does not contain a chroma_db folder. Build its index first, then try again.",
                "Index not found",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        using var chat = new ChatForm(dialog.SelectedPath);
        chat.ShowDialog(this);
    }
}