using System;
using System.Collections.Generic;
using System.Text.Json;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Widget;
using IME.Shared.Security;

namespace IME.Features.Keyboard;

[Activity(Label = "自定义符号", Theme = "@style/MyNoActionBarTheme")]
public class QuickSymbolEditorActivity : SecurityMonitoredActivity
{
    private const string Tag = "QuickSymbolEditor";
    private const string PrefsName = "t9_quick_symbols";
    private const string SymbolsV2Key = "symbols_v2";

    protected override void OnResume()
    {
        base.OnResume();
        IME.Features.Settings.KamiVipVerificationCoordinator.Start(this, nameof(QuickSymbolEditorActivity));
    }

    protected override void OnPause()
    {
        IME.Features.Settings.KamiVipVerificationCoordinator.Stop();
        base.OnPause();
    }

    private EditText? _editDisplay;
    private EditText? _editCommit;
    private LinearLayout? _listContainer;
    private List<QuickSymbolEntry> _entries = new();

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        if (!EnsureSecurityAllowedNow())
        {
            return;
        }

        _entries = LoadEntries();

        var root = BuildUI();
        SetContentView(root);
        RenderList();
    }

    private ScrollView BuildUI()
    {
        var scroll = new ScrollView(this)
        {
            LayoutParameters = new ViewGroup.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent)
        };

        var outer = new LinearLayout(this)
        {
            Orientation = Orientation.Vertical,
            LayoutParameters = new ViewGroup.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
        };
        int pad = DpToPx(16);
        outer.SetPadding(pad, pad, pad, pad);

        // 标题
        var title = new TextView(this)
        {
            Text = "自定义快捷符号",
            LayoutParameters = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
        };
        title.SetTextSize(ComplexUnitType.Sp, 20f);
        title.SetTextColor(Color.White);
        title.Gravity = GravityFlags.Center;
        var titleParams = (LinearLayout.LayoutParams)title.LayoutParameters;
        titleParams.BottomMargin = DpToPx(16);
        outer.AddView(title);

        // 说明
        var desc = new TextView(this)
        {
            Text = "显示符号：按钮上显示的文字\n上屏内容：点击后实际输入的内容",
            LayoutParameters = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
        };
        desc.SetTextSize(ComplexUnitType.Sp, 13f);
        desc.SetTextColor(Color.LightGray);
        var descParams = (LinearLayout.LayoutParams)desc.LayoutParameters;
        descParams.BottomMargin = DpToPx(12);
        outer.AddView(desc);

        // 输入区
        var inputRow = new LinearLayout(this)
        {
            Orientation = Orientation.Horizontal,
            LayoutParameters = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
        };

        _editDisplay = new EditText(this)
        {
            Hint = "显示符号",
            LayoutParameters = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f)
        };
        _editDisplay.SetTextSize(ComplexUnitType.Sp, 15f);
        _editDisplay.SetSingleLine(true);
        var editDisplayParams = (LinearLayout.LayoutParams)_editDisplay.LayoutParameters;
        editDisplayParams.RightMargin = DpToPx(4);
        inputRow.AddView(_editDisplay);

        _editCommit = new EditText(this)
        {
            Hint = "上屏内容",
            LayoutParameters = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f)
        };
        _editCommit.SetTextSize(ComplexUnitType.Sp, 15f);
        _editCommit.SetSingleLine(true);
        var editCommitParams = (LinearLayout.LayoutParams)_editCommit.LayoutParameters;
        editCommitParams.LeftMargin = DpToPx(4);
        inputRow.AddView(_editCommit);

        outer.AddView(inputRow);

        // 添加按钮
        var btnAdd = new Button(this)
        {
            Text = "添加",
            LayoutParameters = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
        };
        var btnAddParams = (LinearLayout.LayoutParams)btnAdd.LayoutParameters;
        btnAddParams.TopMargin = DpToPx(8);
        btnAddParams.BottomMargin = DpToPx(16);
        btnAdd.Click += OnAddClick;
        outer.AddView(btnAdd);

        // 分隔线
        var divider = new View(this)
        {
            LayoutParameters = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, DpToPx(1))
        };
        divider.SetBackgroundColor(Color.Gray);
        var dividerParams = (LinearLayout.LayoutParams)divider.LayoutParameters;
        dividerParams.BottomMargin = DpToPx(12);
        outer.AddView(divider);

        // 列表标题
        var listTitle = new TextView(this)
        {
            Text = "当前符号列表",
            LayoutParameters = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
        };
        listTitle.SetTextSize(ComplexUnitType.Sp, 16f);
        listTitle.SetTextColor(Color.White);
        var listTitleParams = (LinearLayout.LayoutParams)listTitle.LayoutParameters;
        listTitleParams.BottomMargin = DpToPx(8);
        outer.AddView(listTitle);

        // 符号列表容器
        _listContainer = new LinearLayout(this)
        {
            Orientation = Orientation.Vertical,
            LayoutParameters = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
        };
        outer.AddView(_listContainer);

        scroll.AddView(outer);
        return scroll;
    }

    private void OnAddClick(object? sender, EventArgs e)
    {
        string display = _editDisplay?.Text?.Trim() ?? "";
        string commit = _editCommit?.Text?.Trim() ?? "";

        if (string.IsNullOrEmpty(display))
        {
            Toast.MakeText(this, "请输入显示符号", ToastLength.Short)?.Show();
            return;
        }

        if (string.IsNullOrEmpty(commit))
        {
            commit = display;
        }

        _entries.Add(new QuickSymbolEntry { Display = display, Commit = commit });
        SaveEntries();
        RenderList();

        _editDisplay!.Text = "";
        _editCommit!.Text = "";

        Toast.MakeText(this, "已添加", ToastLength.Short)?.Show();
    }

    private void RenderList()
    {
        if (_listContainer == null)
        {
            return;
        }

        _listContainer.RemoveAllViews();

        for (int i = 0; i < _entries.Count; i++)
        {
            var entry = _entries[i];
            int index = i;

            var row = new LinearLayout(this)
            {
                Orientation = Orientation.Horizontal,
                LayoutParameters = new LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
            };
            row.SetGravity(GravityFlags.CenterVertical);
            row.SetPadding(0, DpToPx(4), 0, DpToPx(4));

            // 显示 → 上屏
            string labelText = entry.Display == entry.Commit
                ? entry.Display
                : $"{entry.Display} → {entry.Commit}";

            var label = new TextView(this)
            {
                Text = labelText,
                LayoutParameters = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f)
            };
            label.SetTextSize(ComplexUnitType.Sp, 15f);
            label.SetTextColor(Color.White);
            row.AddView(label);

            // 删除按钮
            var btnDelete = new Button(this)
            {
                Text = "删除",
                LayoutParameters = new LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent)
            };
            btnDelete.SetTextSize(ComplexUnitType.Sp, 13f);
            btnDelete.Click += (_, _) =>
            {
                _entries.RemoveAt(index);
                SaveEntries();
                RenderList();
                Toast.MakeText(this, "已删除", ToastLength.Short)?.Show();
            };
            row.AddView(btnDelete);

            _listContainer.AddView(row);
        }

        if (_entries.Count == 0)
        {
            var empty = new TextView(this)
            {
                Text = "暂无自定义符号",
                LayoutParameters = new LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
            };
            empty.SetTextSize(ComplexUnitType.Sp, 14f);
            empty.SetTextColor(Color.Gray);
            empty.Gravity = GravityFlags.Center;
            var emptyParams = (LinearLayout.LayoutParams)empty.LayoutParameters;
            emptyParams.TopMargin = DpToPx(16);
            _listContainer.AddView(empty);
        }
    }

    private List<QuickSymbolEntry> LoadEntries()
    {
        try
        {
            var prefs = GetSharedPreferences(PrefsName, FileCreationMode.Private);
            string? json = prefs?.GetString(SymbolsV2Key, null);
            if (!string.IsNullOrEmpty(json))
            {
                var entries = JsonSerializer.Deserialize<List<QuickSymbolEntry>>(json);
                if (entries != null)
                {
                    return entries;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warn(Tag, $"Failed to load entries: {ex.Message}");
        }

        return new List<QuickSymbolEntry>();
    }

    private void SaveEntries()
    {
        try
        {
            var prefs = GetSharedPreferences(PrefsName, FileCreationMode.Private);
            var editor = prefs?.Edit();
            string json = JsonSerializer.Serialize(_entries);
            editor?.PutString(SymbolsV2Key, json);
            editor?.Apply();
        }
        catch (Exception ex)
        {
            Log.Warn(Tag, $"Failed to save entries: {ex.Message}");
        }
    }

    private int DpToPx(float dp)
    {
        return (int)TypedValue.ApplyDimension(ComplexUnitType.Dip, dp, Resources.DisplayMetrics);
    }
}
