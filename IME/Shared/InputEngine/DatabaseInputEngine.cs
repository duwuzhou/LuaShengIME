using System;
using System.Collections.Generic;
using Android.Content;
using Android.Util;
using IME.Features.UserLexicon;
using IME.Shared.Abstractions;

namespace IME.Shared.InputEngine
{
    /// <summary>
    /// 基于数据库的输入引擎，用于短语快速方式输入。
    /// </summary>
    public class DatabaseInputEngine : IInputEngine
    {
        private string _composingText = "";
        private List<string> _candidates = new List<string>();
        private bool _initialized = false;
        private bool _asciiMode = false;
        private LocalUserLexiconStore _lexiconStore;

        public DatabaseInputEngine()
        {
        }

        public bool Initialize(Context context)
        {
            if (_initialized) return true;

            try
            {
                if (context == null)
                {
                    return false;
                }

                _lexiconStore ??= new LocalUserLexiconStore(context);
                _initialized = true;
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("DatabaseInputEngine", $"初始化失败: {ex.Message}");
                return false;
            }
        }

        public bool ProcessKey(int keyCode)
        {
            if (!_initialized) return false;

            try
            {
                // 退格处理
                if (keyCode == 8 || keyCode == (int)Android.Views.Keycode.Del)
                {
                    if (_composingText.Length > 0)
                    {
                        _composingText = _composingText.Substring(0, _composingText.Length - 1);
                        UpdateCandidates();
                        return true;
                    }
                    return false;
                }

                // 将按键转换为字符并添加到组合文本
                char c = (char)keyCode;
                _composingText += c;

                // 更新候选词
                UpdateCandidates();

                return true;
            }
            catch (Exception ex)
            {
                Log.Error("DatabaseInputEngine", $"澶勭悊鎸夐敭澶辫触: {ex.Message}");
                return false;
            }
        }

        public string GetComposingText()
        {
            return _composingText;
        }

        public List<string> GetCandidates()
        {
            return new List<string>(_candidates);
        }

        public List<string> GetCandidateComments()
        {
            var comments = new List<string>();
            for (int i = 0; i < _candidates.Count; i++)
            {
                comments.Add(string.Empty);
            }
            return comments;
        }

        public string SelectCandidate(int index)
        {
            if (index < 0 || index >= _candidates.Count)
            {
                return "";
            }

            string selectedCandidate = _candidates[index];

            // 重置状态
            Reset();

            return selectedCandidate;
        }

        public void Reset()
        {
            _composingText = "";
            _candidates.Clear();
        }

        public void SetAsciiMode(bool asciiMode)
        {
            _asciiMode = asciiMode;
            // 数据库引擎不需要额外处理
        }

        public bool GetAsciiMode()
        {
            return _asciiMode;
        }

        public void SetSimplification(bool simplified)
        {
            // 数据库引擎不涉及繁简转换
        }

        public bool SupportsPaging => false;

        public bool TryGetPagingInfo(out int pageNo, out bool isLastPage, out int pageSize)
        {
            pageNo = 0;
            isLastPage = true;
            pageSize = _candidates?.Count ?? 0;
            return false;
        }

        public bool ChangePage(bool backward)
        {
            return false;
        }

        public void Dispose()
        {
            _lexiconStore?.Dispose();
            _lexiconStore = null;
            _initialized = false;
        }

        /// <summary>
        /// 更新候选词列表
        /// </summary>
        private void UpdateCandidates()
        {
            _candidates.Clear();

            if (string.IsNullOrEmpty(_composingText))
            {
                return;
            }

            try
            {
                var userCandidates = GetUserLexiconCandidates(_composingText);
                if (userCandidates.Count > 0)
                {
                    _candidates.AddRange(userCandidates);
                }
            }
            catch (Exception ex)
            {
                Log.Error("DatabaseInputEngine", $"更新候选词失败: {ex.Message}");
            }
        }

        private List<string> GetUserLexiconCandidates(string pinyin)
        {
            if (_lexiconStore == null || string.IsNullOrWhiteSpace(pinyin))
            {
                return new List<string>();
            }

            var entries = _lexiconStore.QueryByPinyinPrefix(pinyin, 10);
            var results = new List<string>();
            foreach (var entry in entries)
            {
                if (!string.IsNullOrWhiteSpace(entry.Word))
                {
                    results.Add(entry.Word);
                }
            }

            return results;
        }
    }
}





