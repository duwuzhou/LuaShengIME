using System;
using System.Collections.Generic;
using IME.Features.Input;
using IME.Shared.Abstractions;

namespace IME.Features.Candidates
{
    public partial class Candidate
    {
        public void SetInput(string input)
        {
            _inputString = input;
            UpdateCandidates();
        }

        public void SetCandidates(List<string> candidates)
        {
            LoadCandidateSettings();
            SetCandidatesWithExtras(candidates, _inputEngine?.GetCandidateComments(), null);
        }

        internal void SetCandidatesWithExtras(List<string> candidates, List<string> comments, List<CandidateEntry> extras)
        {
            LoadCandidateSettings();
            ResetCandidateEntries(candidates, comments);
            AppendExtraEntries(extras);
            ShowCandidates(true);
            RefreshPanelIfOpen();
        }

        private void UpdateCandidates()
        {
            if (_inputEngine != null && !string.IsNullOrEmpty(_inputString))
            {
                LoadCandidateSettings();
                var candidates = _inputEngine.GetCandidates();
                var comments = _inputEngine.GetCandidateComments();
                ResetCandidateEntries(candidates, comments);
            }
            else
            {
                _candidateEntries.Clear();
            }

            ShowCandidates(true);
            RefreshPanelIfOpen();
        }

        private void ShowCandidates(bool resetScroll)
        {
            NotifyCandidateListChanged();

            if (_candidateEntries.Count == 0)
            {
                UpdateCandidatePreview(string.Empty);
                UpdateUI();
                return;
            }

            UpdateCandidatePreview(GetPrimaryCandidateText());
            if (resetScroll && _candidateLayoutManager.FindFirstVisibleItemPosition() > 0)
            {
                _candidateRecyclerView.ScrollToPosition(0);
            }

            UpdateUI();
        }

        private void NotifyCandidateListChanged()
        {
            _candidateAdapter?.NotifyDataSetChanged();
        }

        private void NotifyCandidateItemsInserted(int startIndex)
        {
            if (_candidateAdapter == null)
            {
                return;
            }

            int safeStart = Math.Max(0, startIndex);
            int count = _candidateEntries.Count - safeStart;
            if (count <= 0)
            {
                return;
            }

            _candidateAdapter.NotifyItemRangeInserted(safeStart, count);
        }

        private string GetPrimaryCandidateText()
        {
            for (int i = 0; i < _candidateEntries.Count; i++)
            {
                if (_candidateEntries[i].PageIndex == _currentPageIndex)
                {
                    return _candidateEntries[i].Text;
                }
            }

            return _candidateEntries[0].Text;
        }

        private void OnCandidateItemClicked(int position)
        {
            CandidateEntry entry;
            lock (_candidateLock)
            {
                if (position < 0 || position >= _candidateEntries.Count)
                {
                    return;
                }
                entry = _candidateEntries[position];
            }
            OnCandidateSelected?.Invoke(entry.Text, entry.IndexOnPage);
            CommitCandidateEntry(entry);
            UpdateCandidatePreview(entry.Text);
        }

        internal void CommitCandidateEntry(CandidateEntry entry)
        {
            ExitPinyinEditMode();

            if (_inputEngine == null)
            {
                return;
            }

            string composingText = _inputEngine.GetComposingText();
            if (string.IsNullOrWhiteSpace(composingText))
            {
                composingText = _inputPreviewRawText;
            }

            if (entry.IsPrediction)
            {
                if (!string.IsNullOrEmpty(entry.Text))
                {
                    _imeService?.CommitText(entry.Text, true);
                }

                _inputEngine.Reset();
                Clear();
                _keyboardHandler?.NotifyT9BufferShouldReset();
                return;
            }

            if (entry.IsLocal)
            {
                if (!string.IsNullOrEmpty(entry.Text))
                {
                    _imeService?.CommitText(entry.Text, false);
                    if (!string.IsNullOrWhiteSpace(composingText))
                    {
                        _imeService?.RecordUserLexicon(entry.Text, composingText);
                    }
                }

                _inputEngine.Reset();
                Clear();
                _keyboardHandler?.NotifyT9BufferShouldReset();
                return;
            }

            if (_inputEngine.SupportsPaging && !EnsurePage(entry.PageIndex))
            {
                return;
            }

            string committed = _inputEngine.SelectCandidate(entry.IndexOnPage);
            if (!string.IsNullOrEmpty(committed))
            {
                _imeService?.CommitText(committed, false);
                if (!string.IsNullOrWhiteSpace(composingText))
                {
                    _imeService?.RecordUserLexicon(committed, composingText);
                }
            }

            string remaining = _inputEngine.GetComposingText();
            if (string.IsNullOrEmpty(remaining))
            {
                Clear();
                _keyboardHandler?.NotifyT9BufferShouldReset();
            }
            else
            {
                if (_keyboardHandler != null)
                {
                    _keyboardHandler.UpdateChineseCandidates();
                }
                else
                {
                    SetInputPreview(remaining);
                    SetCandidates(_inputEngine.GetCandidates());
                }
            }
        }

        public void SetImeService(Ime imeService)
        {
            _imeService = imeService;
        }

        public void SetInputEngine(IInputEngine engine)
        {
            _inputEngine = engine;
        }

        public void Clear()
        {
            _inputString = string.Empty;
            lock (_candidateLock)
            {
                _candidateEntries.Clear();
            }
            NotifyCandidateListChanged();
            ExitPinyinEditMode();
            _inputPreviewRawText = string.Empty;
            _inputPreviewText.Text = string.Empty;
            UpdateCandidatePreview(string.Empty);
            _candidateRecyclerView.ScrollToPosition(0);
            UpdateUI();
            RefreshPanelIfOpen();
        }

        public int GetCandidateCount()
        {
            return _candidateEntries.Count;
        }

        private void ResetCandidateEntries(List<string> candidates, List<string> comments)
        {
            lock (_candidateLock)
            {
                _candidateEntries.Clear();
                _currentPageIndex = 0;
                _isLastPage = false;

                if (candidates == null || candidates.Count == 0)
                {
                    return;
                }

                UpdatePagingInfoFromEngine();
                AppendPageEntriesLocked(_currentPageIndex, candidates, comments);
            }
        }

        private void AppendPageEntries(int pageIndex, List<string> candidates, List<string> comments)
        {
            lock (_candidateLock)
            {
                AppendPageEntriesLocked(pageIndex, candidates, comments);
            }
        }

        private void AppendPageEntriesLocked(int pageIndex, List<string> candidates, List<string> comments)
        {
            if (candidates == null)
            {
                return;
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                string candidateText = candidates[i] ?? string.Empty;
                if (ShouldHideNumericCandidates() && IsNumericLikeCandidate(candidateText))
                {
                    continue;
                }

                string comment = string.Empty;
                if (comments != null && i < comments.Count)
                {
                    comment = comments[i] ?? string.Empty;
                }

                _candidateEntries.Add(new CandidateEntry(candidateText, comment, pageIndex, i));
            }
        }

        private void AppendExtraEntries(List<CandidateEntry> extras)
        {
            if (extras == null || extras.Count == 0)
            {
                return;
            }

            lock (_candidateLock)
            {
                int insertIndex = 0;
                foreach (var entry in extras)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.Text))
                    {
                        continue;
                    }

                    _candidateEntries.Insert(insertIndex, entry);
                    insertIndex++;
                }
            }
        }

        internal string GetInputPreviewText()
        {
            return _inputPreviewText?.Text ?? string.Empty;
        }

        internal string GetCandidatePreviewText()
        {
            return _candidatePreviewText?.Text ?? string.Empty;
        }

        internal List<CandidateEntry> GetCandidateEntriesSnapshot()
        {
            lock (_candidateLock)
            {
                return new List<CandidateEntry>(_candidateEntries);
            }
        }

        private bool ShouldHideNumericCandidates()
        {
            return _keyboardHandler != null
                && _keyboardHandler.IsT9Mode
                && !_keyboardHandler.GetAsciiMode();
        }

        private static bool IsNumericLikeCandidate(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            bool hasDigit = false;
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (char.IsDigit(ch))
                {
                    hasDigit = true;
                    continue;
                }

                if (char.IsLetter(ch))
                {
                    return false;
                }

                if (char.IsWhiteSpace(ch) || char.IsPunctuation(ch) || char.IsSymbol(ch))
                {
                    continue;
                }

                return false;
            }

            return hasDigit;
        }
    }
}
