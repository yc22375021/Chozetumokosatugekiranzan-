using System.Drawing;
using System.Windows.Forms;

namespace Chozetumokosatugekiranzan;

internal sealed class CharacterSelectForm : Form
{
    private readonly CharacterImageStore _store;
    private readonly ListBox _roster = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly PictureBox _portrait = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(26, 35, 49) };
    private readonly PictureBox _icon = new() { Size = new Size(64, 64), SizeMode = PictureBoxSizeMode.Zoom };
    private readonly Label _name = new() { AutoSize = true, Font = new Font("Yu Gothic UI", 24, FontStyle.Bold) };
    private readonly Label _description = new() { Dock = DockStyle.Fill, AutoSize = true, MaximumSize = new Size(440, 0) };
    private readonly Label _status = new() { Dock = DockStyle.Fill, AutoSize = true, ForeColor = Color.Gold };
    private readonly Label _empty = new() { Text = "画像未登録\n「画像を取り込む」で追加", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
    private readonly ComboBox _role = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 190 };
    private readonly Button _confirm = new() { Text = "このキャラクターで決定", AutoSize = true, Height = 42 };
    private bool _confirming;
    public CharacterDefinition Selected => CharacterCatalog.All[_roster.SelectedIndex];
    private CharacterImage Role => (CharacterImage)Math.Max(0, _role.SelectedIndex);

    public CharacterSelectForm(CharacterImageStore store, int selectedId = 1)
    {
        _store = store;
        Text = "Fighter — キャラクター選択";
        ClientSize = new Size(960, 650);
        MinimumSize = new Size(800, 620);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Yu Gothic UI", 11);
        BackColor = Color.FromArgb(9, 17, 29);
        ForeColor = Color.WhiteSmoke;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 2, RowCount = 4 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
        Controls.Add(root);
        var title = new Label { Text = "CHARACTER SELECT / キャラクター選択", AutoSize = true, ForeColor = Color.FromArgb(81, 221, 250), Font = new Font(Font, FontStyle.Bold) };
        root.Controls.Add(title, 0, 0); root.SetColumnSpan(title, 2);
        _portrait.Controls.Add(_empty);
        root.Controls.Add(_portrait, 0, 1);
        var details = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24, 0, 0, 0), RowCount = 6, ColumnCount = 1 };
        details.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        details.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        details.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        details.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        details.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        details.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
        root.Controls.Add(details, 1, 1);
        details.Controls.Add(_name, 0, 0); details.Controls.Add(_description, 0, 1);
        details.Controls.Add(new Label { Text = "表示・取り込み先", AutoSize = true }, 0, 2);
        details.Controls.Add(_role, 0, 3);
        var imports = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        var import = MakeButton("画像を取り込む", ImportImage);
        var remove = MakeButton("この画像を解除", RemoveImage);
        imports.Controls.Add(import); imports.Controls.Add(remove); details.Controls.Add(imports, 0, 4);
        details.Controls.Add(_icon, 0, 5);
        _roster.BackColor = Color.FromArgb(22, 35, 52); _roster.ForeColor = Color.White;
        foreach (var c in CharacterCatalog.All) _roster.Items.Add($"{c.Id:D4}  {c.Name}" + (c.Playable ? "" : "（プレビュー）"));
        root.Controls.Add(_roster, 0, 2); root.Controls.Add(_status, 1, 2);
        var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
        var cancel = MakeButton("終了", (_, _) => Close());
        _confirm.Click += Confirm;
        StyleButton(_confirm);
        footer.Controls.Add(_confirm); footer.Controls.Add(cancel);
        root.Controls.Add(footer, 0, 3); root.SetColumnSpan(footer, 2);
        AcceptButton = _confirm; CancelButton = cancel;
        _roster.SelectedIndexChanged += (_, _) => SelectCharacter();
        _role.SelectedIndexChanged += (_, _) => { _confirming = false; RefreshImages(); };
        _roster.SelectedIndex = Math.Max(0, Array.FindIndex(CharacterCatalog.All, c => c.Id == selectedId));
    }
    private static void StyleButton(Button button)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.BackColor = Color.FromArgb(25, 47, 63);
        button.ForeColor = Color.White;
        button.Margin = new Padding(4);
        button.Padding = new Padding(8);
    }
    private static Button MakeButton(string text, EventHandler handler)
    {
        var b = new Button { Text = text, AutoSize = true }; StyleButton(b); b.Click += handler; return b;
    }
    private void SelectCharacter()
    {
        _confirming = false;
        _name.Text = Selected.Name;
        _description.Text = Selected.Description;
        _role.Items.Clear();
        _role.Items.AddRange([Selected.Poses[0], Selected.Poses[1], "顔アイコン", "対戦用の静止画"]);
        _role.SelectedIndex = 0;
    }
    private void RefreshImages()
    {
        _confirm.Text = "このキャラクターで決定";
        _confirm.Enabled = Selected.Playable;
        _status.Text = Selected.Playable ? "対戦相手：CPU（同キャラクター）" : "専用の技・戦闘処理は未実装です。画像の登録とプレビューができます。";
        if (Role == CharacterImage.Battle) _status.Text += "\n対戦用画像は静止画です。モーションや当たり判定は変更しません。";
        try
        {
            ReplaceImage(_portrait, _store.Load(Selected.Id, Role));
            ReplaceImage(_icon, _store.Load(Selected.Id, CharacterImage.Icon));
        }
        catch (Exception e) when (CharacterImageStore.IsImageError(e))
        {
            ReplaceImage(_portrait, null); ReplaceImage(_icon, null);
            _status.Text = "保存画像を読めません。再取り込みしてください。\n" + e.Message;
        }
        _empty.Visible = _portrait.Image is null;
    }
    private static void ReplaceImage(PictureBox box, Image? next)
    {
        var old = box.Image; box.Image = next; old?.Dispose();
    }
    private void ImportImage(object? sender, EventArgs e)
    {
        using var picker = new OpenFileDialog { Title = $"{Selected.Name}：{_role.Text}", Filter = "画像（PNG / JPEG）|*.png;*.jpg;*.jpeg", CheckFileExists = true, Multiselect = false };
        if (picker.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            _store.Import(Selected.Id, Role, picker.FileName);
            _confirming = false; RefreshImages();
            _status.Text = "取り込み・保存しました。元の画像は移動しても大丈夫です。";
        }
        catch (Exception error) when (CharacterImageStore.IsImageError(error))
        {
            MessageBox.Show(this, "取り込めませんでした。登録済みの画像は保持します。\n" + error.Message, "画像取り込み", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
    private void RemoveImage(object? sender, EventArgs e)
    {
        if (MessageBox.Show(this, "この登録画像を解除しますか？ 元のファイルは削除しません。", "画像を解除", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        try { _store.Remove(Selected.Id, Role); _confirming = false; RefreshImages(); }
        catch (Exception error) when (CharacterImageStore.IsImageError(error)) { MessageBox.Show(this, error.Message, "解除できませんでした"); }
    }
    private void Confirm(object? sender, EventArgs e)
    {
        if (!Selected.Playable) return;
        if (!_confirming)
        {
            _confirming = true;
            _confirm.Text = "対戦を開始 →";
            _status.Text = $"PLAYER 1：{Selected.Name}\nVS CPU：{Selected.Name}\n選び直す場合はキャラクターを選択してください。";
            return;
        }
        DialogResult = DialogResult.OK;
        Close();
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { ReplaceImage(_portrait, null); ReplaceImage(_icon, null); }
        base.Dispose(disposing);
    }
}
