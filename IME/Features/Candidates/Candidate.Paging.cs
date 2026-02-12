using Android.Util;

namespace IME.Features.Candidates
{
    public partial class Candidate
    {
        private bool EnsurePage(int targetPage)
        {
            if (_inputEngine == null || !_inputEngine.SupportsPaging)
            {
                return true;
            }

            int safety = 32;
            while (_currentPageIndex < targetPage && safety-- > 0)
            {
                if (!_inputEngine.ChangePage(false))
                {
                    return false;
                }

                _currentPageIndex++;
            }

            while (_currentPageIndex > targetPage && safety-- > 0)
            {
                if (!_inputEngine.ChangePage(true))
                {
                    return false;
                }

                _currentPageIndex--;
            }

            return true;
        }

        private void UpdatePagingInfoFromEngine()
        {
            if (_inputEngine == null)
            {
                return;
            }

            if (_inputEngine.TryGetPagingInfo(out int pageNo, out bool isLast, out int pageSize))
            {
                _currentPageIndex = pageNo;
                _isLastPage = isLast;
                if (pageSize > 0)
                {
                    _pageSize = pageSize;
                }
            }
        }

        private void SetupScrollListener()
        {
            if (_candidateRecyclerView == null)
            {
                return;
            }

            _candidateScrollListener ??= new CandidateScrollListener(this);
            _candidateRecyclerView.ClearOnScrollListeners();
            _candidateRecyclerView.AddOnScrollListener(_candidateScrollListener);
        }

        private void TryLoadNextPageIfNeeded()
        {
            if (_inputEngine == null || !_inputEngine.SupportsPaging || _isLoadingPage || _isLastPage)
            {
                return;
            }

            if (_candidateLayoutManager == null)
            {
                return;
            }

            int lastVisible = _candidateLayoutManager.FindLastVisibleItemPosition();
            if (lastVisible < 0)
            {
                return;
            }

            int remainingItems = _candidateEntries.Count - 1 - lastVisible;
            if (remainingItems > 1)
            {
                return;
            }

            if (TryLoadNextPage(true))
            {
                RefreshPanelIfOpen();
            }
        }

        internal bool TryLoadNextPage()
        {
            return TryLoadNextPage(false);
        }

        private bool TryLoadNextPage(bool appendViews)
        {
            if (_inputEngine == null || !_inputEngine.SupportsPaging)
            {
                return false;
            }

            if (_isLoadingPage || _isLastPage)
            {
                return false;
            }

            Log.Info(Tag, $"TryLoadNextPage start: page={_currentPageIndex}, last={_isLastPage}, count={_candidateEntries.Count}");

            _isLoadingPage = true;
            try
            {
                int startIndex = _candidateEntries.Count;
                bool changed = _inputEngine.ChangePage(false);
                if (!changed)
                {
                    Log.Info(Tag, "TryLoadNextPage: ChangePage(false) returned false");
                    return false;
                }

                var candidates = _inputEngine.GetCandidates();
                var comments = _inputEngine.GetCandidateComments();
                UpdatePagingInfoFromEngine();
                Log.Info(Tag, $"TryLoadNextPage done: page={_currentPageIndex}, last={_isLastPage}, newCount={candidates.Count}");
                AppendPageEntries(_currentPageIndex, candidates, comments);

                if (appendViews)
                {
                    NotifyCandidateItemsInserted(startIndex);
                    UpdateCandidatePreview(GetPrimaryCandidateText());
                    UpdateUI();
                }

                return true;
            }
            finally
            {
                _isLoadingPage = false;
            }
        }

        internal bool HasMorePages()
        {
            return _inputEngine != null && _inputEngine.SupportsPaging && !_isLastPage;
        }
    }
}
