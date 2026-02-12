using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Views;
using Android.Widget;
using AndroidUri = Android.Net.Uri;
using IME.Shared.Data;

namespace IME.Features.Shortcuts;

[Activity(Label = "@string/shortcut_manager_title", Theme = "@style/MyNoActionBarTheme")]
public class ShortcutManagerActivity : Activity
{
    private const int RequestImport = 2301;
    private const int RequestExport = 2302;

    private Spinner _spinnerCategory;
    private EditText _editCategory;
    private EditText _editCategoryRename;
    private EditText _editPhrase;
    private Button _btnAddCategory;
    private Button _btnRenameCategory;
    private Button _btnDeleteCategory;
    private Button _btnAddPhrase;
    private Button _btnImport;
    private Button _btnExport;
    private ListView _listView;
    private TextView _tvEmpty;

    private readonly List<string> _phraseItems = new();
    private ShortcutPhraseAdapter _phraseAdapter;
    private SwordDBHelper _dbHelper;

    private bool _suppressCategoryChanged;
    private bool _isDestroyed;
    private int _phraseLoadRequestId;

    protected override void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SetContentView(Resource.Layout.activity_shortcut_manager);

        _dbHelper = new SwordDBHelper(this);

        _spinnerCategory = FindViewById<Spinner>(Resource.Id.spinner_shortcut_category);
        _editCategory = FindViewById<EditText>(Resource.Id.edit_shortcut_category);
        _editCategoryRename = FindViewById<EditText>(Resource.Id.edit_shortcut_rename);
        _editPhrase = FindViewById<EditText>(Resource.Id.edit_shortcut_phrase);
        _btnAddCategory = FindViewById<Button>(Resource.Id.btn_shortcut_add_category);
        _btnRenameCategory = FindViewById<Button>(Resource.Id.btn_shortcut_rename_category);
        _btnDeleteCategory = FindViewById<Button>(Resource.Id.btn_shortcut_delete_category);
        _btnAddPhrase = FindViewById<Button>(Resource.Id.btn_shortcut_add_phrase);
        _btnImport = FindViewById<Button>(Resource.Id.btn_shortcut_import);
        _btnExport = FindViewById<Button>(Resource.Id.btn_shortcut_export);
        _listView = FindViewById<ListView>(Resource.Id.list_shortcut_items);
        _tvEmpty = FindViewById<TextView>(Resource.Id.tv_shortcut_empty);

        _phraseAdapter = new ShortcutPhraseAdapter(this, _phraseItems, DeletePhrase);
        _listView.Adapter = _phraseAdapter;
        BindListTouchForNestedScroll();

        BindEvents();
        LoadCategories();
    }

    protected override void OnDestroy()
    {
        _isDestroyed = true;
        base.OnDestroy();
    }

    private void BindListTouchForNestedScroll()
    {
        _listView.Touch += (_, e) =>
        {
            if (e.Event == null)
            {
                return;
            }

            switch (e.Event.ActionMasked)
            {
                case MotionEventActions.Down:
                case MotionEventActions.Move:
                    ToggleParentIntercept(disallow: true);
                    break;
                case MotionEventActions.Up:
                case MotionEventActions.Cancel:
                    ToggleParentIntercept(disallow: false);
                    break;
            }

            e.Handled = false;
        };
    }

    private void ToggleParentIntercept(bool disallow)
    {
        IViewParent? parent = _listView.Parent;
        while (parent != null)
        {
            parent.RequestDisallowInterceptTouchEvent(disallow);
            parent = parent.Parent;
        }
    }

    private void BindEvents()
    {
        _spinnerCategory.ItemSelected += (_, _) =>
        {
            if (_suppressCategoryChanged)
            {
                return;
            }

            _editCategoryRename.Text = GetSelectedCategory();
            RenderPhraseList();
        };

        _btnAddCategory.Click += (_, _) => AddCategory();
        _btnRenameCategory.Click += (_, _) => RenameCurrentCategory();
        _btnDeleteCategory.Click += (_, _) => DeleteCurrentCategory();
        _btnAddPhrase.Click += (_, _) => AddPhrase();
        _btnImport.Click += (_, _) => StartImport();
        _btnExport.Click += (_, _) => StartExport();
    }

    private async void LoadCategories(string preferredCategory = null)
    {
        ShowLoadingMessage();

        List<string> categories;
        try
        {
            categories = await Task.Run(() =>
            {
                var result = _dbHelper.QueryCategories();
                result.Sort(StringComparer.Ordinal);
                return result;
            });
        }
        catch (Exception ex)
        {
            if (_isDestroyed)
            {
                return;
            }

            ShowToast(GetString(Resource.String.shortcut_msg_load_failed_prefix) + ex.Message);
            ShowEmptyMessage(GetString(Resource.String.shortcut_msg_add_group_first));
            return;
        }

        if (_isDestroyed)
        {
            return;
        }

        _suppressCategoryChanged = true;

        var adapter = new ArrayAdapter<string>(this, Android.Resource.Layout.SimpleSpinnerItem, categories);
        adapter.SetDropDownViewResource(Android.Resource.Layout.SimpleSpinnerDropDownItem);
        _spinnerCategory.Adapter = adapter;

        bool hasCategory = categories.Count > 0;
        _spinnerCategory.Enabled = hasCategory;
        _btnRenameCategory.Enabled = hasCategory;
        _btnDeleteCategory.Enabled = hasCategory;
        _btnAddPhrase.Enabled = hasCategory;
        _btnExport.Enabled = hasCategory;
        _editCategoryRename.Enabled = hasCategory;

        if (!hasCategory)
        {
            _editCategoryRename.Text = string.Empty;
            _phraseItems.Clear();
            _phraseAdapter.NotifyDataSetChanged();

            _suppressCategoryChanged = false;
            ShowEmptyMessage(GetString(Resource.String.shortcut_msg_add_group_first));
            return;
        }

        int index = 0;
        if (!string.IsNullOrWhiteSpace(preferredCategory))
        {
            int matched = categories.IndexOf(preferredCategory);
            if (matched >= 0)
            {
                index = matched;
            }
        }

        _spinnerCategory.SetSelection(index);
        _editCategoryRename.Text = categories[index];

        _suppressCategoryChanged = false;
        RenderPhraseList();
    }

    private void AddCategory()
    {
        string category = Normalize(_editCategory.Text);
        if (string.IsNullOrEmpty(category))
        {
            ShowToast(GetString(Resource.String.shortcut_msg_enter_group_name));
            return;
        }

        bool added = _dbHelper.AddCategory(category);
        if (!added)
        {
            ShowToast(GetString(Resource.String.shortcut_msg_group_exists));
            return;
        }

        _editCategory.Text = string.Empty;
        ShowToast(GetString(Resource.String.shortcut_msg_group_added));
        LoadCategories(category);
    }

    private void RenameCurrentCategory()
    {
        string oldName = GetSelectedCategory();
        if (string.IsNullOrEmpty(oldName))
        {
            ShowToast(GetString(Resource.String.shortcut_msg_choose_group_first));
            return;
        }

        string newName = Normalize(_editCategoryRename.Text);
        if (string.IsNullOrEmpty(newName))
        {
            ShowToast(GetString(Resource.String.shortcut_msg_enter_new_group_name));
            return;
        }

        bool renamed = _dbHelper.RenameCategory(oldName, newName);
        if (!renamed)
        {
            ShowToast(GetString(Resource.String.shortcut_msg_rename_failed));
            return;
        }

        ShowToast(GetString(Resource.String.shortcut_msg_group_renamed));
        LoadCategories(newName);
    }

    private void DeleteCurrentCategory()
    {
        string category = GetSelectedCategory();
        if (string.IsNullOrEmpty(category))
        {
            ShowToast(GetString(Resource.String.shortcut_msg_choose_group_first));
            return;
        }

        new AlertDialog.Builder(this)
            .SetTitle(Resource.String.shortcut_dialog_delete_group_title)
            .SetMessage(string.Format(GetString(Resource.String.shortcut_dialog_delete_group_message), category))
            .SetPositiveButton(Resource.String.shortcut_delete_group, (_, _) =>
            {
                int deleted = _dbHelper.DeleteCategory(category);
                ShowToast(deleted > 0
                    ? GetString(Resource.String.shortcut_msg_group_deleted)
                    : GetString(Resource.String.shortcut_msg_delete_failed));
                LoadCategories();
            })
            .SetNegativeButton(Resource.String.shortcut_cancel, (_, _) => { })
            .Show();
    }

    private void AddPhrase()
    {
        string category = GetSelectedCategory();
        if (string.IsNullOrEmpty(category))
        {
            ShowToast(GetString(Resource.String.shortcut_msg_create_group_first));
            return;
        }

        string phrase = Normalize(_editPhrase.Text);
        if (string.IsNullOrEmpty(phrase))
        {
            ShowToast(GetString(Resource.String.shortcut_msg_enter_phrase));
            return;
        }

        bool added = _dbHelper.AddItem(category, phrase);
        if (!added)
        {
            ShowToast(GetString(Resource.String.shortcut_msg_phrase_exists));
            return;
        }

        _editPhrase.Text = string.Empty;
        ShowToast(GetString(Resource.String.shortcut_msg_phrase_added));
        RenderPhraseList();
    }

    private void StartImport()
    {
        var intent = new Intent(Intent.ActionOpenDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType("text/plain");
        StartActivityForResult(intent, RequestImport);
    }

    private void StartExport()
    {
        var intent = new Intent(Intent.ActionCreateDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType("text/plain");
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        intent.PutExtra(Intent.ExtraTitle, $"ime_shortcuts_{timestamp}.txt");
        StartActivityForResult(intent, RequestExport);
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
    {
        base.OnActivityResult(requestCode, resultCode, data);

        if (resultCode != Result.Ok || data?.Data == null)
        {
            return;
        }

        AndroidUri uri = data.Data;
        switch (requestCode)
        {
            case RequestImport:
                ImportFromUri(uri);
                break;
            case RequestExport:
                ExportToUri(uri);
                break;
        }
    }

    private void ImportFromUri(AndroidUri uri)
    {
        try
        {
            using var stream = ContentResolver.OpenInputStream(uri);
            if (stream == null)
            {
                ShowToast(GetString(Resource.String.shortcut_msg_cannot_open_file));
                return;
            }

            using var reader = new StreamReader(stream, Encoding.UTF8, true);
            string currentCategory = string.Empty;
            int addedCategories = 0;
            int addedPhrases = 0;

            while (!reader.EndOfStream)
            {
                string raw = reader.ReadLine();
                string line = Normalize(raw);
                if (string.IsNullOrEmpty(line) || line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                if (line.StartsWith("[", StringComparison.Ordinal) &&
                    line.EndsWith("]", StringComparison.Ordinal) &&
                    line.Length > 2)
                {
                    currentCategory = Normalize(line.Substring(1, line.Length - 2));
                    if (!string.IsNullOrEmpty(currentCategory) && _dbHelper.AddCategory(currentCategory))
                    {
                        addedCategories++;
                    }

                    continue;
                }

                string category = currentCategory;
                string phrase = line;
                int tabIndex = line.IndexOf('	');
                if (tabIndex > 0)
                {
                    category = Normalize(line.Substring(0, tabIndex));
                    phrase = Normalize(line.Substring(tabIndex + 1));
                }

                if (string.IsNullOrEmpty(category) || string.IsNullOrEmpty(phrase))
                {
                    continue;
                }

                if (_dbHelper.AddItem(category, phrase))
                {
                    addedPhrases++;
                }
            }

            LoadCategories(currentCategory);
            ShowToast(string.Format(GetString(Resource.String.shortcut_msg_import_done), addedCategories, addedPhrases));
        }
        catch (Exception ex)
        {
            ShowToast(GetString(Resource.String.shortcut_msg_import_failed_prefix) + ex.Message);
        }
    }

    private void ExportToUri(AndroidUri uri)
    {
        try
        {
            using var stream = ContentResolver.OpenOutputStream(uri);
            if (stream == null)
            {
                ShowToast(GetString(Resource.String.shortcut_msg_cannot_create_file));
                return;
            }

            var categories = _dbHelper.QueryCategories();
            categories.Sort(StringComparer.Ordinal);

            using var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, leaveOpen: true);
            writer.WriteLine("# IME 快捷短语");
            writer.WriteLine("# 格式：[分组名]，后续每行一个短语");
            writer.WriteLine();

            int total = 0;
            foreach (string category in categories)
            {
                writer.WriteLine($"[{category}]");
                var items = _dbHelper.QueryItemsByCategory(category);
                items.Sort(StringComparer.Ordinal);
                foreach (string item in items)
                {
                    writer.WriteLine(item);
                    total++;
                }

                writer.WriteLine();
            }

            writer.Flush();
            ShowToast(string.Format(GetString(Resource.String.shortcut_msg_export_done), categories.Count, total));
        }
        catch (Exception ex)
        {
            ShowToast(GetString(Resource.String.shortcut_msg_export_failed_prefix) + ex.Message);
        }
    }

    private async void RenderPhraseList()
    {
        string category = GetSelectedCategory();
        if (string.IsNullOrEmpty(category))
        {
            _phraseItems.Clear();
            _phraseAdapter.NotifyDataSetChanged();
            ShowEmptyMessage(GetString(Resource.String.shortcut_msg_add_group_first));
            return;
        }

        int requestId = ++_phraseLoadRequestId;
        ShowLoadingMessage();

        List<string> items;
        try
        {
            items = await Task.Run(() =>
            {
                var result = _dbHelper.QueryItemsByCategory(category);
                result.Sort(StringComparer.Ordinal);
                return result;
            });
        }
        catch (Exception ex)
        {
            if (_isDestroyed || requestId != _phraseLoadRequestId)
            {
                return;
            }

            ShowToast(GetString(Resource.String.shortcut_msg_load_failed_prefix) + ex.Message);
            ShowEmptyMessage(GetString(Resource.String.shortcut_msg_no_phrases));
            return;
        }

        if (_isDestroyed || requestId != _phraseLoadRequestId)
        {
            return;
        }

        _phraseItems.Clear();
        _phraseItems.AddRange(items);
        _phraseAdapter.NotifyDataSetChanged();

        if (_phraseItems.Count == 0)
        {
            ShowEmptyMessage(GetString(Resource.String.shortcut_msg_no_phrases));
            return;
        }

        ShowPhraseList();
    }

    private void DeletePhrase(string phrase)
    {
        string category = GetSelectedCategory();
        if (string.IsNullOrEmpty(category) || string.IsNullOrEmpty(phrase))
        {
            return;
        }

        int deleted = _dbHelper.DeleteItem(category, phrase);
        if (deleted > 0)
        {
            _phraseItems.Remove(phrase);
            _phraseAdapter.NotifyDataSetChanged();

            if (_phraseItems.Count == 0)
            {
                ShowEmptyMessage(GetString(Resource.String.shortcut_msg_no_phrases));
            }
            else
            {
                ShowPhraseList();
            }

            ShowToast(GetString(Resource.String.shortcut_msg_phrase_deleted));
        }
        else
        {
            ShowToast(GetString(Resource.String.shortcut_msg_delete_failed));
        }
    }

    private void ShowLoadingMessage()
    {
        _listView.Visibility = ViewStates.Gone;
        _tvEmpty.Text = GetString(Resource.String.shortcut_msg_loading);
        _tvEmpty.Visibility = ViewStates.Visible;
    }

    private void ShowPhraseList()
    {
        _tvEmpty.Visibility = ViewStates.Gone;
        _listView.Visibility = ViewStates.Visible;
    }

    private void ShowEmptyMessage(string message)
    {
        _listView.Visibility = ViewStates.Gone;
        _tvEmpty.Text = message;
        _tvEmpty.Visibility = ViewStates.Visible;
    }

    private string GetSelectedCategory()
    {
        return _spinnerCategory.SelectedItem?.ToString() ?? string.Empty;
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private void ShowToast(string message)
    {
        Toast.MakeText(this, message, ToastLength.Short).Show();
    }
}
