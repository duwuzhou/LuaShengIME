namespace IME.Features.Candidates
{
    internal class CandidateEntry
    {
        public string Text { get; }
        public string Comment { get; }
        public int PageIndex { get; }
        public int IndexOnPage { get; }
        public bool IsLocal { get; }
        public bool IsPrediction { get; }

        public CandidateEntry(string text, string comment, int pageIndex, int indexOnPage, bool isLocal = false, bool isPrediction = false)
        {
            Text = text ?? string.Empty;
            Comment = comment ?? string.Empty;
            PageIndex = pageIndex;
            IndexOnPage = indexOnPage;
            IsLocal = isLocal;
            IsPrediction = isPrediction;
        }
    }
}
