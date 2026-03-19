using System;
using System.Collections.Generic;
using Android.App;
using Android.Views;
using Android.Widget;
using IME.Shared.ResourceProtection;

namespace IME.Features.Shortcuts;

internal sealed class ShortcutPhraseAdapter : BaseAdapter<string>
{
    private readonly Activity _activity;
    private readonly IList<string> _items;
    private readonly Action<string> _onDeleteClicked;

    public ShortcutPhraseAdapter(Activity activity, IList<string> items, Action<string> onDeleteClicked)
    {
        _activity = activity;
        _items = items;
        _onDeleteClicked = onDeleteClicked;
    }

    public override int Count => _items.Count;

    public override string this[int position] => _items[position];

    public override long GetItemId(int position)
    {
        return position;
    }

    public override View GetView(int position, View convertView, ViewGroup parent)
    {
        View row = convertView;
        if (row == null)
        {
#if DEBUG
            row = _activity.LayoutInflater.Inflate(Resource.Layout.item_shortcut_phrase, parent, false);
#else
            row = EncryptedLayout.Inflate(_activity.LayoutInflater, "layout/item_shortcut_phrase", Resource.Layout.item_shortcut_phrase, parent, false);
#endif
        }
        var holder = row.Tag as ViewHolder;
        if (holder == null)
        {
            holder = new ViewHolder(row, _onDeleteClicked);
            row.Tag = holder;
        }

        holder.Bind(_items[position]);
        return row;
    }

    private sealed class ViewHolder : Java.Lang.Object
    {
        private readonly Action<string> _onDeleteClicked;
        private string _phrase = string.Empty;

        private TextView PhraseView { get; }
        private Button DeleteButton { get; }

        public ViewHolder(View row, Action<string> onDeleteClicked)
        {
            _onDeleteClicked = onDeleteClicked;
            PhraseView = row.FindViewById<TextView>(Resource.Id.tv_shortcut_phrase_content);
            DeleteButton = row.FindViewById<Button>(Resource.Id.btn_shortcut_phrase_delete);

            DeleteButton.Click += (_, _) =>
            {
                if (!string.IsNullOrEmpty(_phrase))
                {
                    _onDeleteClicked(_phrase);
                }
            };
        }

        public void Bind(string phrase)
        {
            _phrase = phrase;
            PhraseView.Text = phrase;
        }
    }
}
