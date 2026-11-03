namespace AITeach1;

public partial class IndexForm : Form
{
    private readonly Services.RagIndexBuilder indexBuilder = new();
    private readonly Panel welcomeCard = new();
    private readonly Label folderLabel = new();
    private readonly Label statusLabel = new();
    private readonly ProgressBar progressBar = new();
    private readonly TextBox activityLog = new();
    private readonly Button indexButton = new();
    private readonly ToolTip folderToolTip = new();
    private CancellationTokenSource? indexingCancellation;

    public IndexForm()
    {
        InitializeComponent();

        Text = "AITeach1 | Local Document RAG Indexer";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1080, 760);
        MinimumSize = new Size(980, 740);
        BackColor = Color.FromArgb(243, 246, 251);
        Font = new Font("Segoe UI", 10F);

        var surface = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = BackColor
        };
        Controls.Add(surface);

        welcomeCard.Size = new Size(900, 650);
        welcomeCard.BackColor = Color.White;
        welcomeCard.Anchor = AnchorStyles.None;
        surface.Controls.Add(welcomeCard);
        surface.Resize += (_, _) => CenterCard(surface);
        CenterCard(surface);

        var eyebrowLabel = new Label
        {
            AutoSize = true,
            Location = new Point(46, 38),
            ForeColor = Color.FromArgb(37, 99, 235),
            Font = new Font("Segoe UI Semibold", 9F),
            Text = "PRIVATE, ON-DEVICE DOCUMENT INDEXING"
        };

        var titleLabel = new Label
        {
            AutoSize = false,
            Location = new Point(44, 76),
            Size = new Size(810, 54),
            ForeColor = Color.FromArgb(20, 33, 61),
            Font = new Font("Segoe UI", 27F, FontStyle.Bold),
            Text = "Build a local RAG index"
        };

        var descriptionLabel = new Label
        {
            AutoSize = false,
            Location = new Point(46, 140),
            Size = new Size(800, 48),
            ForeColor = Color.FromArgb(91, 103, 125),
            Font = new Font("Segoe UI", 10.5F),
            Text = "Choose a folder to read .doc, .docx, .pdf, and .txt files, create Ollama embeddings,\nand save the searchable ChromaDB database alongside your documents."
        };

        indexButton.Location = new Point(46, 216);
        indexButton.Size = new Size(270, 50);
        indexButton.BackColor = Color.FromArgb(37, 99, 235);
        indexButton.ForeColor = Color.White;
        indexButton.FlatStyle = FlatStyle.Flat;
        indexButton.Font = new Font("Segoe UI Semibold", 10F);
        indexButton.Text = "Select folder & build index";
        indexButton.Cursor = Cursors.Hand;
        indexButton.UseVisualStyleBackColor = false;
        indexButton.FlatAppearance.BorderSize = 0;
        indexButton.Click += async (_, _) => await SelectFolderAndIndexAsync();

        var folderCaption = new Label
        {
            AutoSize = true,
            Location = new Point(46, 294),
            ForeColor = Color.FromArgb(20, 33, 61),
            Font = new Font("Segoe UI Semibold", 10F),
            Text = "SELECTED FOLDER"
        };

        folderLabel.AutoSize = false;
        folderLabel.Location = new Point(46, 324);
        folderLabel.Size = new Size(805, 28);
        folderLabel.ForeColor = Color.FromArgb(91, 103, 125);
        folderLabel.Font = new Font("Segoe UI", 9.5F);
        folderLabel.Text = "No folder selected yet.";

        statusLabel.AutoSize = false;
        statusLabel.Location = new Point(46, 376);
        statusLabel.Size = new Size(805, 25);
        statusLabel.ForeColor = Color.FromArgb(20, 33, 61);
        statusLabel.Font = new Font("Segoe UI Semibold", 9.5F);
        statusLabel.Text = "Ready. Ollama and the ChromaDB CLI must be installed and available.";

        progressBar.Location = new Point(46, 408);
        progressBar.Size = new Size(805, 12);
        progressBar.Style = ProgressBarStyle.Continuous;
        progressBar.ForeColor = Color.FromArgb(37, 99, 235);

        var activityCaption = new Label
        {
            AutoSize = true,
            Location = new Point(46, 446),
            ForeColor = Color.FromArgb(20, 33, 61),
            Font = new Font("Segoe UI Semibold", 10F),
            Text = "ACTIVITY"
        };

        activityLog.Location = new Point(46, 476);
        activityLog.Size = new Size(805, 125);
        activityLog.Multiline = true;
        activityLog.ReadOnly = true;
        activityLog.WordWrap = false;
        activityLog.ScrollBars = ScrollBars.Vertical;
        activityLog.BackColor = Color.FromArgb(248, 250, 253);
        activityLog.ForeColor = Color.FromArgb(67, 79, 101);
        activityLog.BorderStyle = BorderStyle.FixedSingle;
        activityLog.Font = new Font("Consolas", 9F);

        var footerLabel = new Label
        {
            AutoSize = true,
            Location = new Point(46, 615),
            ForeColor = Color.FromArgb(130, 141, 160),
            Font = new Font("Segoe UI", 9F),
            Text = "Embeddings: Ollama / nomic-embed-text   •   Vector store: ChromaDB"
        };

        welcomeCard.Controls.AddRange(
        [
            eyebrowLabel,
            titleLabel,
            descriptionLabel,
            indexButton,
            folderCaption,
            folderLabel,
            statusLabel,
            progressBar,
            activityCaption,
            activityLog,
            footerLabel
        ]);

        FormClosed += (_, _) =>
        {
            indexingCancellation?.Cancel();
            indexBuilder.Dispose();
            folderToolTip.Dispose();
        };
    }

    private async Task SelectFolderAndIndexAsync()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select a folder containing .doc, .docx, .pdf, and/or .txt files.",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true
        };

        if (dialog.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(dialog.SelectedPath))
        {
            return;
        }

        folderLabel.Text = dialog.SelectedPath;
        folderToolTip.SetToolTip(folderLabel, dialog.SelectedPath);
        activityLog.Clear();
        AppendLog($"Folder: {dialog.SelectedPath}");
        progressBar.Value = 0;
        indexButton.Enabled = false;
        indexButton.Text = "Building index…";
        indexingCancellation = new CancellationTokenSource();

        var progress = new Progress<Services.IndexProgress>(item =>
        {
            statusLabel.Text = item.Message;
            progressBar.Value = Math.Clamp(item.Percent, 0, 100);
            AppendLog(item.Message);
        });

        try
        {
            await indexBuilder.BuildAsync(dialog.SelectedPath, progress, indexingCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            statusLabel.Text = "Indexing cancelled.";
            AppendLog(statusLabel.Text);
        }
        catch (Exception exception)
        {
            statusLabel.Text = "Indexing failed. See the activity log for details.";
            AppendLog($"ERROR: {exception.Message}");
        }
        finally
        {
            indexingCancellation.Dispose();
            indexingCancellation = null;
            indexButton.Enabled = true;
            indexButton.Text = "Select folder & build index";
        }
    }

    private void AppendLog(string message)
    {
        activityLog.AppendText($"{DateTime.Now:HH:mm:ss}  {message}{Environment.NewLine}");
    }

    private void CenterCard(Control surface)
    {
        welcomeCard.Location = new Point(
            Math.Max(0, (surface.ClientSize.Width - welcomeCard.Width) / 2),
            Math.Max(0, (surface.ClientSize.Height - welcomeCard.Height) / 2));
    }
}