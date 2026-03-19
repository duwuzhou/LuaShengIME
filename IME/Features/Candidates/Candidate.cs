using Android.Content;
using Android.Util;
using Android.Views;
using Android.Widget;
using AndroidX.RecyclerView.Widget;
using IME.Features.Keyboard;
using IME.Shared.ResourceProtection;
using System;

namespace IME.Features.Candidates
{
    public partial class Candidate : LinearLayout
    {
        public Candidate(Context context) : base(context)
        {
            Initialize(context, null);
        }

        public Candidate(Context context, IAttributeSet attrs) : base(context, attrs)
        {
            Initialize(context, attrs);
        }

        public Candidate(Context context, IAttributeSet attrs, KeyboardHandler keyboardHandler) : base(context, attrs)
        {
            _keyboardHandler = keyboardHandler;
            Initialize(context, attrs);
        }

        public Candidate(Context context, IAttributeSet attrs, int defStyle) : base(context, attrs, defStyle)
        {
            Initialize(context, attrs);
        }

        private void Initialize(Context context, IAttributeSet attrs)
        {
            Orientation = Orientation.Vertical;

            LayoutInflater inflater = (LayoutInflater)context.GetSystemService(Context.LayoutInflaterService);
            int candidateLayoutId = ResolveLayoutId(context, "candidate");
#if DEBUG
            inflater.Inflate(candidateLayoutId, this, true);
#else
            EncryptedLayout.Inflate(inflater, "layout/candidate", candidateLayoutId, this, true);
#endif

            _inputPreviewContainer = FindViewById<LinearLayout>(Resource.Id.inputPreviewContainer);
            _inputPreviewText = FindViewById<TextView>(Resource.Id.inputPreviewText);
            _candidatePreviewText = FindViewById<TextView>(Resource.Id.candidatePreviewText);
            _candidateRecyclerView = FindViewById<RecyclerView>(Resource.Id.candidateRecyclerView);
            _functionButtonsContainer = FindViewById<LinearLayout>(Resource.Id.functionButtonsContainer);
            _switchButton1 = FindViewById<Button>(Resource.Id.switchButton1);
            _switchButton2 = FindViewById<Button>(Resource.Id.switchButton2);
            _candidateRow = FindViewById<LinearLayout>(Resource.Id.candidateRow);
            _btnCandidateMore = FindViewById<Button>(Resource.Id.btn_candidate_more);
            _btnPinyinEditBack = FindViewById<Button>(Resource.Id.btnPinyinEditBack);

            _candidateLayoutManager = new LinearLayoutManager(context, LinearLayoutManager.Horizontal, false);
            _candidateRecyclerView.SetLayoutManager(_candidateLayoutManager);
            _candidateRecyclerView.HasFixedSize = true;
            _candidateRecyclerView.SetItemAnimator(null);
            _candidateRecyclerView.NestedScrollingEnabled = false;
            _candidateAdapter = new CandidateChipAdapter(this);
            _candidateRecyclerView.SetAdapter(_candidateAdapter);

            _switchButton1.Click += OnSwitchButton1Click;
            _switchButton2.Click += OnSwitchButton2Click;
            _btnCandidateMore.Click += (s, e) => OpenCandidatePanel();
            _btnPinyinEditBack.Click += OnPinyinEditBackClick;
            _inputPreviewText.Touch += OnInputPreviewTouch;
            _inputPreviewText.Click += OnInputPreviewClick;

            LoadCandidateSettings();
            SetupScrollListener();
            UpdateUI();
        }

        protected override void OnDetachedFromWindow()
        {
            StopCursorBlink();
            if (_cursorBlinkHandler != null)
            {
                _cursorBlinkHandler.RemoveCallbacksAndMessages(null);
                _cursorBlinkHandler = null;
            }
            _cursorBlinkRunnable = null;
            _candidatePanel?.Cleanup();
            _candidatePanel = null;
            base.OnDetachedFromWindow();
        }

        private static int ResolveLayoutId(Context context, string layoutName)
        {
            int layoutId = context.Resources?.GetIdentifier(layoutName, "layout", context.PackageName) ?? 0;
            if (layoutId == 0)
            {
                throw new InvalidOperationException($"Layout resource not found: {layoutName}");
            }

            return layoutId;
        }
    }
}
