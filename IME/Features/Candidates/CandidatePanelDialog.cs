using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Util;
using Android.Views;
using Android.Widget;
using System;
using System.Collections.Generic;
using Android.OS;

namespace IME.Features.Candidates
{
    internal class CandidatePanelPopup
    {
        private readonly Candidate _host;
        private readonly Context _context;
        private PopupWindow _popup;
        private View _rootView;
        private LinearLayout _headerView;
        private ScrollView _scrollView;
        private LinearLayout _listContainer;
        private TextView _inputPreviewText;
        private TextView _candidatePreviewText;
        private Button _closeButton;
        private Button _loadMoreButton;
        private int _renderedCount = 0;
        private GestureDetector _gestureDetector;
        private long _lastLoadTick = 0;

        public CandidatePanelPopup(Context context, Candidate host)
        {
            _context = context;
            _host = host;
            CreatePopup();
        }

        public bool IsShowing => _popup != null && _popup.IsShowing;

        public void Show(View anchor)
        {
            if (_popup == null)
                CreatePopup();

            int screenHeight = _context.Resources.DisplayMetrics.HeightPixels;
            int height = (int)(screenHeight * 0.5f);
            _popup.Height = height;
            _popup.Width = ViewGroup.LayoutParams.MatchParent;
            _popup.ShowAtLocation(anchor, GravityFlags.Bottom, 0, 0);
            RefreshFromHost();
        }

        public void Dismiss()
        {
            _popup?.Dismiss();
        }

        internal void Cleanup()
        {
            Dismiss();
            if (_scrollView != null)
            {
                _scrollView.ViewTreeObserver.ScrollChanged -= OnScrollChanged;
                _scrollView.Touch -= OnScrollTouch;
            }
            _gestureDetector = null;
            _popup = null;
            _rootView = null;
        }

        private void CreatePopup()
        {
            _rootView = LayoutInflater.From(_context).Inflate(Resource.Layout.CandidatePanel, null);
            _popup = new PopupWindow(_rootView, ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent, true);
            _popup.Focusable = true;
            _popup.OutsideTouchable = true;
            _popup.SetBackgroundDrawable(new ColorDrawable(Color.Transparent));

            _headerView = _rootView.FindViewById<LinearLayout>(Resource.Id.panelHeader);
            _scrollView = _rootView.FindViewById<ScrollView>(Resource.Id.panelScrollView);
            _listContainer = _rootView.FindViewById<LinearLayout>(Resource.Id.panelListContainer);
            _inputPreviewText = _rootView.FindViewById<TextView>(Resource.Id.panelInputPreviewText);
            _candidatePreviewText = _rootView.FindViewById<TextView>(Resource.Id.panelCandidatePreviewText);
            _closeButton = _rootView.FindViewById<Button>(Resource.Id.btnPanelClose);

            _closeButton.Click += (s, e) => Dismiss();
            _scrollView.ViewTreeObserver.ScrollChanged += OnScrollChanged;
            _scrollView.Touch += OnScrollTouch;
            SetupHeaderGesture();
            SetupLoadMoreButton();
        }

        private void SetupLoadMoreButton()
        {
            _loadMoreButton = new Button(_context)
            {
                Text = "加载更多"
            };
            _loadMoreButton.SetTextSize(ComplexUnitType.Sp, 12);
            _loadMoreButton.Visibility = ViewStates.Gone;
            _loadMoreButton.Click += (s, e) =>
            {
                if (_host.TryLoadNextPage())
                {
                    AppendNewEntries();
                    UpdatePreview();
                }
                UpdateLoadMoreVisibility();
            };

            if (_headerView != null)
            {
                int insertIndex = Math.Max(1, _headerView.ChildCount - 1);
                _headerView.AddView(_loadMoreButton, insertIndex);
            }
        }

        private void SetupHeaderGesture()
        {
            if (_headerView == null)
                return;

            _gestureDetector = new GestureDetector(_context, new HeaderGestureListener(this));
            _headerView.Touch += (s, e) =>
            {
                _gestureDetector?.OnTouchEvent(e.Event);
                e.Handled = false;
            };
        }

        private void OnScrollChanged(object sender, EventArgs e)
        {
            MaybeLoadMore();
        }

        private void OnScrollTouch(object sender, View.TouchEventArgs e)
        {
            if (e.Event.Action == MotionEventActions.Up || e.Event.Action == MotionEventActions.Move)
            {
                if (SystemClock.UptimeMillis() - _lastLoadTick > 250)
                {
                    MaybeLoadMore();
                }
            }
            e.Handled = false;
        }

        private void MaybeLoadMore()
        {
            if (_scrollView == null || _listContainer == null)
                return;

            int threshold = (int)TypedValue.ApplyDimension(ComplexUnitType.Dip, 48, _context.Resources.DisplayMetrics);
            int bottomEdge = _scrollView.ScrollY + _scrollView.Height;
            if (bottomEdge >= _listContainer.Height - threshold)
            {
                if (_host.TryLoadNextPage())
                {
                    AppendNewEntries();
                    UpdatePreview();
                    _lastLoadTick = SystemClock.UptimeMillis();
                }
                UpdateLoadMoreVisibility();
            }
        }

        internal void RefreshFromHost()
        {
            if (_listContainer == null)
                return;

            _listContainer.RemoveAllViews();
            _renderedCount = 0;
            UpdatePreview();
            AppendNewEntries();
            UpdateLoadMoreVisibility();
            EnsureContentFill();
        }

        private void UpdatePreview()
        {
            if (_inputPreviewText != null)
                _inputPreviewText.Text = _host.GetInputPreviewText();
            if (_candidatePreviewText != null)
                _candidatePreviewText.Text = _host.GetCandidatePreviewText();
        }

        private void AppendNewEntries()
        {
            List<CandidateEntry> entries = _host.GetCandidateEntriesSnapshot();
            if (entries.Count <= _renderedCount)
                return;

            for (int i = _renderedCount; i < entries.Count; i++)
            {
                AddEntryView(entries[i], i);
            }

            _renderedCount = entries.Count;
        }

        private void UpdateLoadMoreVisibility()
        {
            if (_loadMoreButton == null)
                return;

            _loadMoreButton.Visibility = _host.HasMorePages() ? ViewStates.Visible : ViewStates.Gone;
        }

        private void EnsureContentFill()
        {
            if (_scrollView == null || _listContainer == null)
                return;

            if (_scrollView.Height == 0 || _listContainer.Height == 0)
            {
                _scrollView.Post(EnsureContentFill);
                return;
            }

            int safety = 6;
            while (_listContainer.Height <= _scrollView.Height && safety-- > 0)
            {
                if (!_host.TryLoadNextPage())
                    break;

                AppendNewEntries();
                _listContainer.RequestLayout();
            }
        }

        private void AddEntryView(CandidateEntry entry, int displayIndex)
        {
            var context = _context;

            var itemLayout = new LinearLayout(context)
            {
                Orientation = Orientation.Horizontal
            };
            itemLayout.SetGravity(GravityFlags.CenterVertical);
            itemLayout.SetPadding(8, 8, 8, 8);

            var indexText = new TextView(context)
            {
                Text = (displayIndex + 1).ToString()
            };
            indexText.SetTextColor(Color.Gray);
            indexText.SetTextSize(ComplexUnitType.Sp, 12);
            indexText.SetPadding(0, 0, 12, 0);

            var textContainer = new LinearLayout(context)
            {
                Orientation = Orientation.Vertical
            };

            var candidateText = new TextView(context)
            {
                Text = entry.Text
            };
            candidateText.SetTextColor(Color.Black);
            candidateText.SetTextSize(ComplexUnitType.Sp, 18);

            var commentText = new TextView(context)
            {
                Text = entry.Comment ?? string.Empty
            };
            commentText.SetTextColor(Color.Gray);
            commentText.SetTextSize(ComplexUnitType.Sp, 12);
            commentText.Visibility = string.IsNullOrEmpty(entry.Comment) ? ViewStates.Gone : ViewStates.Visible;

            textContainer.AddView(candidateText);
            textContainer.AddView(commentText);

            itemLayout.AddView(indexText);
            itemLayout.AddView(textContainer);

            itemLayout.Click += (s, e) =>
            {
                _host.CommitCandidateEntry(entry);
                Dismiss();
            };

            var attrs = new int[] { Android.Resource.Attribute.SelectableItemBackground };
            var ta = context.ObtainStyledAttributes(attrs);
            var drawable = ta.GetDrawable(0);
            ta.Recycle();
            itemLayout.Background = drawable;

            _listContainer.AddView(itemLayout);
        }

        private class HeaderGestureListener : GestureDetector.SimpleOnGestureListener
        {
            private readonly CandidatePanelPopup _owner;
            private const int MinDistance = 60;
            private const int MinVelocity = 200;

            public HeaderGestureListener(CandidatePanelPopup owner)
            {
                _owner = owner;
            }

            public override bool OnFling(MotionEvent e1, MotionEvent e2, float velocityX, float velocityY)
            {
                if (e1 == null || e2 == null)
                    return false;

                float dy = e2.GetY() - e1.GetY();
                if (dy > MinDistance && Math.Abs(velocityY) > MinVelocity)
                {
                    _owner.Dismiss();
                    return true;
                }

                return false;
            }
        }
    }
}
