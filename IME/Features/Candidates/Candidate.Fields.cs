using Android.Graphics;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Widget;
using AndroidX.Core.Content;
using AndroidX.RecyclerView.Widget;
using System;
using System.Collections.Generic;
using IME.Features.Input;
using IME.Features.Keyboard;
using IME.Shared.Abstractions;

namespace IME.Features.Candidates
{
    public partial class Candidate
    {
        private const string Tag = "Candidate";
        private const int CandidateSettingsRefreshIntervalMs = 750;

        private LinearLayout _inputPreviewContainer;
        private TextView _inputPreviewText;
        private TextView _candidatePreviewText;
        private RecyclerView _candidateRecyclerView;
        private LinearLayout _functionButtonsContainer;
        private Button _switchButton1;
        private Button _switchButton2;
        private LinearLayout _candidateRow;
        private Button _btnCandidateMore;
        private Button _btnPinyinEditBack;

        public event EventHandler OnKeyboardClick;
        public event EventHandler OnFunctionClick;
        public event EventHandler OnModeClick;
        public event CandidateSelectedEventHandler OnCandidateSelected;

        private string _inputString = string.Empty;
        private string _inputPreviewRawText = string.Empty;
        private readonly List<CandidateEntry> _candidateEntries = new List<CandidateEntry>();
        private readonly object _candidateLock = new object();
        private int _currentPageIndex = 0;
        private int _pageSize = 6;
        private bool _isLastPage = false;
        private bool _isLoadingPage = false;
        private bool _previewEnabled = true;
        private bool _candidateSettingsLoaded;
        private long _lastCandidateSettingsRefreshAt;

        private LinearLayoutManager _candidateLayoutManager;
        private CandidateChipAdapter _candidateAdapter;
        private CandidateScrollListener _candidateScrollListener;

        private KeyboardHandler _keyboardHandler;
        private IInputEngine _inputEngine;
        private CandidatePanelPopup _candidatePanel;
        private Ime _imeService;

        private bool _isPinyinEditing;
        private string _editablePinyin = string.Empty;
        private int _editCursorIndex;
        private float _lastInputPreviewTouchX;
        private Handler? _cursorBlinkHandler;
        private Java.Lang.IRunnable? _cursorBlinkRunnable;
        private bool _isEditCursorVisible = true;
        private bool _isEditVisualApplied;
        private string _lastRenderedPinyin = string.Empty;
        private int _lastRenderedCursor = -1;
        private bool _lastRenderedCursorVisible = true;

        private sealed class CandidateChipViewHolder : RecyclerView.ViewHolder
        {
            private readonly Action<int> _onItemClick;

            public CandidateChipViewHolder(LinearLayout itemLayout, TextView indexText, TextView candidateText, Action<int> onItemClick)
                : base(itemLayout)
            {
                ItemLayout = itemLayout;
                IndexText = indexText;
                CandidateText = candidateText;
                _onItemClick = onItemClick;

                itemLayout.Click += (_, _) =>
                {
                    int position = AdapterPosition;
                    if (position != RecyclerView.NoPosition)
                    {
                        _onItemClick(position);
                    }
                };
            }

            public LinearLayout ItemLayout { get; }

            public TextView IndexText { get; }

            public TextView CandidateText { get; }

            public void Bind(CandidateEntry entry, int displayIndex)
            {
                IndexText.Text = string.Empty;
                IndexText.Visibility = ViewStates.Gone;
                CandidateText.Text = entry.Text ?? string.Empty;
            }
        }

        private sealed class CandidateChipAdapter : RecyclerView.Adapter
        {
            private readonly Candidate _host;

            public CandidateChipAdapter(Candidate host)
            {
                _host = host;
            }

            public override int ItemCount
            {
                get
                {
                    lock (_host._candidateLock)
                    {
                        return _host._candidateEntries.Count;
                    }
                }
            }

            public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
            {
                var context = parent.Context;

                var itemLayout = new LinearLayout(context)
                {
                    Orientation = Orientation.Horizontal
                };
                itemLayout.SetGravity(GravityFlags.CenterVertical);
                itemLayout.SetPadding(Dp(context, 12), Dp(context, 8), Dp(context, 12), Dp(context, 8));
                itemLayout.SetBackgroundResource(Resource.Drawable.bg_candidate_chip);

                var layoutParams = new RecyclerView.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent);
                layoutParams.SetMargins(Dp(context, 6), Dp(context, 4), Dp(context, 6), Dp(context, 4));
                itemLayout.LayoutParameters = layoutParams;

                var indexText = new TextView(context)
                {
                    Text = string.Empty
                };
                indexText.SetTextColor(new Color(ContextCompat.GetColor(context, Resource.Color.candidate_index)));
                indexText.SetTextSize(ComplexUnitType.Sp, 12f);
                indexText.SetPadding(0, 0, Dp(context, 6), 0);

                var candidateText = new TextView(context)
                {
                    Text = string.Empty
                };
                candidateText.SetTextColor(new Color(ContextCompat.GetColor(context, Resource.Color.candidate_text)));
                candidateText.SetTextSize(ComplexUnitType.Sp, 18f);

                itemLayout.AddView(indexText);
                itemLayout.AddView(candidateText);

                return new CandidateChipViewHolder(itemLayout, indexText, candidateText, _host.OnCandidateItemClicked);
            }

            public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
            {
                if (holder is not CandidateChipViewHolder chipHolder)
                {
                    return;
                }

                CandidateEntry? entry;
                lock (_host._candidateLock)
                {
                    if (position < 0 || position >= _host._candidateEntries.Count)
                        return;
                    entry = _host._candidateEntries[position];
                }
                chipHolder.Bind(entry, position);
            }

            private static int Dp(Android.Content.Context context, int value)
            {
                return (int)TypedValue.ApplyDimension(ComplexUnitType.Dip, value, context.Resources.DisplayMetrics);
            }
        }

        private sealed class CandidateScrollListener : RecyclerView.OnScrollListener
        {
            private readonly Candidate _host;

            public CandidateScrollListener(Candidate host)
            {
                _host = host;
            }

            public override void OnScrolled(RecyclerView recyclerView, int dx, int dy)
            {
                if (dx > 0)
                {
                    _host.TryLoadNextPageIfNeeded();
                }
            }
        }

        public delegate void CandidateSelectedEventHandler(string candidate, int index);
    }
}
