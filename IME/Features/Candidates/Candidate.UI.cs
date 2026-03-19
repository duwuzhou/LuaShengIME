using Android.Util;
using Android.Views;
using System;
using IME.Features.Keyboard;

namespace IME.Features.Candidates
{
    public partial class Candidate
    {
        public void SetInputPreview(string previewText)
        {
            string normalizedPreview = previewText ?? string.Empty;
            if (!_isPinyinEditing && string.Equals(_inputPreviewRawText, normalizedPreview, StringComparison.Ordinal))
            {
                return;
            }

            _inputPreviewRawText = normalizedPreview;

            if (_isPinyinEditing)
            {
                _editCursorIndex = Math.Clamp(_editCursorIndex, 0, _editablePinyin.Length);
            }

            RenderInputPreviewText();
            UpdateUI();
            RefreshPanelIfOpen();
        }

        private void UpdateUI()
        {
            bool hasInput = !string.IsNullOrEmpty(_inputPreviewText?.Text);
            bool hasPreview = _previewEnabled && !string.IsNullOrEmpty(_candidatePreviewText?.Text);
            bool hasCandidates = _candidateEntries.Count > 0;

            if (hasInput || hasPreview || hasCandidates)
            {
                _inputPreviewContainer.Visibility = (hasInput || hasPreview) ? ViewStates.Visible : ViewStates.Gone;
                _candidateRow.Visibility = hasCandidates ? ViewStates.Visible : ViewStates.Invisible;
            }
            else
            {
                _inputPreviewContainer.Visibility = ViewStates.Invisible;
                _candidateRow.Visibility = ViewStates.Invisible;
            }

            _functionButtonsContainer.Visibility = ViewStates.Visible;

            ApplyCandidatePreviewVisibility();
        }

        private void OnSwitchButton1Click(object sender, EventArgs e)
        {
            _keyboardHandler?.SwitchViewgn(GnType.keyborad);
            _keyboardHandler?.ShowLayoutPicker();
            OnKeyboardClick?.Invoke(this, e);
        }

        private void OnSwitchButton2Click(object sender, EventArgs e)
        {
            _keyboardHandler?.ShowFunctionHome();
            _keyboardHandler?.SwitchViewgn(GnType.gn1);
            OnFunctionClick?.Invoke(this, e);
        }

        private void LoadCandidateSettings(bool forceReload = false)
        {
            long now = Environment.TickCount64;
            if (!forceReload
                && _candidateSettingsLoaded
                && (now - _lastCandidateSettingsRefreshAt) < CandidateSettingsRefreshIntervalMs)
            {
                return;
            }

            _candidateSettingsLoaded = true;
            _lastCandidateSettingsRefreshAt = now;

            int newPageSize = Math.Max(1, IME.Features.Settings.SettingsActivity.GetCandidatePageSize(Context));
            bool newPreviewEnabled = IME.Features.Settings.SettingsActivity.GetCandidatePreviewEnabled(Context);

            _pageSize = newPageSize;
            bool previewChanged = _previewEnabled != newPreviewEnabled;
            _previewEnabled = newPreviewEnabled;

            if (_candidatePreviewText != null)
            {
                ViewStates previewVisibility = _previewEnabled ? ViewStates.Visible : ViewStates.Gone;
                if (_candidatePreviewText.Visibility != previewVisibility)
                {
                    _candidatePreviewText.Visibility = previewVisibility;
                }

                if (!_previewEnabled && !string.IsNullOrEmpty(_candidatePreviewText.Text))
                {
                    _candidatePreviewText.Text = string.Empty;
                }
                else if (_previewEnabled && previewChanged && _candidateEntries.Count > 0)
                {
                    _candidatePreviewText.Text = GetPrimaryCandidateText();
                }
            }

            ApplyCandidatePreviewVisibility();
            UpdatePinyinEditVisualState();
        }

        private void UpdateCandidatePreview(string preview)
        {
            if (_candidatePreviewText == null)
            {
                return;
            }

            string nextText = _previewEnabled ? (preview ?? string.Empty) : string.Empty;
            if (string.Equals(_candidatePreviewText.Text, nextText, StringComparison.Ordinal))
            {
                return;
            }

            _candidatePreviewText.Text = nextText;
            ApplyCandidatePreviewVisibility();
        }

        private bool ShouldPrioritizeT9InputPreview()
        {
            if (_keyboardHandler == null)
            {
                return false;
            }

            if (!_keyboardHandler.IsT9Mode || _keyboardHandler.GetAsciiMode())
            {
                return false;
            }

            return !_isPinyinEditing && !string.IsNullOrEmpty(_inputPreviewText?.Text);
        }

        private void ApplyCandidatePreviewVisibility()
        {
            if (_candidatePreviewText == null)
            {
                return;
            }

            if (_isPinyinEditing)
            {
                _candidatePreviewText.Visibility = ViewStates.Gone;
                return;
            }

            if (ShouldPrioritizeT9InputPreview())
            {
                _candidatePreviewText.Visibility = ViewStates.Gone;
                return;
            }

            _candidatePreviewText.Visibility = _previewEnabled ? ViewStates.Visible : ViewStates.Gone;
        }
    }
}
