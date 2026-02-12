
using System;
using System.Collections.Generic;
using Android.Content;

namespace IME.Shared.Abstractions
{
    /// <summary>
    /// 输入引擎接口，定义输入法引擎的核心功能
    /// </summary>
    public interface IInputEngine
    {
        /// <summary>
        /// 初始化引擎
        /// </summary>
        /// <param name="context">Android 上下文</param>
        /// <returns>初始化是否成功</returns>
        bool Initialize(Context context);

        /// <summary>
        /// 处理按键输入
        /// </summary>
        /// <param name="keyCode">按键代码</param>
        /// <returns>是否成功处理该按键</returns>
        bool ProcessKey(int keyCode);

        /// <summary>
        /// 获取当前组合文本（未提交的输入）
        /// </summary>
        /// <returns>组合文本字符串</returns>
        string GetComposingText();

        /// <summary>
        /// 获取候选词列表
        /// </summary>
        /// <returns>候选词列表</returns>
        List<string> GetCandidates();

        /// <summary>
        /// 获取候选词注释（与候选词列表一一对应）
        /// </summary>
        /// <returns>候选词注释列表</returns>
        List<string> GetCandidateComments();

        /// <summary>
        /// 选择候选词
        /// </summary>
        /// <param name="index">候选词索引</param>
        /// <returns>要提交的文本</returns>
        string SelectCandidate(int index);

        /// <summary>
        /// 重置引擎状态
        /// </summary>
        void Reset();

        /// <summary>
        /// 设置中英文模式
        /// </summary>
        /// <param name="asciiMode">true=英文模式，false=中文模式</param>
        void SetAsciiMode(bool asciiMode);

        /// <summary>
        /// 获取当前中英文模式
        /// </summary>
        /// <returns>true=英文模式，false=中文模式</returns>
        bool GetAsciiMode();

        /// <summary>
        /// 设置繁简体模式
        /// </summary>
        /// <param name="simplified">true=简体，false=繁体</param>
        void SetSimplification(bool simplified);

        /// <summary>
        /// 是否支持候选分页
        /// </summary>
        bool SupportsPaging { get; }

        /// <summary>
        /// 获取分页信息（返回 false 表示不可用）
        /// </summary>
        bool TryGetPagingInfo(out int pageNo, out bool isLastPage, out int pageSize);

        /// <summary>
        /// 翻页（true=上一页，false=下一页）
        /// </summary>
        bool ChangePage(bool backward);

        /// <summary>
        /// 清理资源
        /// </summary>
        void Dispose();
    }
}
