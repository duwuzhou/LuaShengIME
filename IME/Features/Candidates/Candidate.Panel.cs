namespace IME.Features.Candidates
{
    public partial class Candidate
    {
        private void OpenCandidatePanel()
        {
            if (_candidatePanel == null)
            {
                _candidatePanel = new CandidatePanelPopup(Context, this);
            }

            if (!_candidatePanel.IsShowing)
            {
                _candidatePanel.Show(this);
            }
        }

        private void RefreshPanelIfOpen()
        {
            if (_candidatePanel != null && _candidatePanel.IsShowing)
            {
                _candidatePanel.RefreshFromHost();
            }
        }
    }
}
