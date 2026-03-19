using System;
using System.Collections.Generic;

namespace IME.Features.Keyboard;

public static class T9PinyinHelper
{
    private static readonly char[][] DigitLetters =
    {
        Array.Empty<char>(), // 0
        Array.Empty<char>(), // 1
        new[] { 'a', 'b', 'c' },
        new[] { 'd', 'e', 'f' },
        new[] { 'g', 'h', 'i' },
        new[] { 'j', 'k', 'l' },
        new[] { 'm', 'n', 'o' },
        new[] { 'p', 'q', 'r', 's' },
        new[] { 't', 'u', 'v' },
        new[] { 'w', 'x', 'y', 'z' }
    };

    private static readonly string[] AllSyllables = GetAllSyllables();
    private static readonly TrieNode SyllableTrie = BuildTrie();
    private static readonly Dictionary<string, int> SyllableFrequency = BuildFrequencyMap();

    public static List<string> GetMatchingSyllables(string digits)
    {
        var results = new List<string>();
        if (string.IsNullOrEmpty(digits)) return results;

        if (digits.Length == 1)
        {
            int digit = digits[0] - '0';
            if (digit >= 2 && digit <= 9)
            {
                foreach (char letter in DigitLetters[digit])
                {
                    results.Add(letter.ToString());
                }
            }
            return results;
        }

        SearchTrie(digits, 0, SyllableTrie, "", results);

        if (digits.Length > 1)
        {
            var multiCharOnly = results.FindAll(item => item.Length > 1);
            if (multiCharOnly.Count > 0)
            {
                results = multiCharOnly;
            }
        }

        // 按频率降序；频率相同优先更长音节，最后按字母序
        results.Sort((a, b) =>
        {
            SyllableFrequency.TryGetValue(a, out int freqA);
            SyllableFrequency.TryGetValue(b, out int freqB);
            int cmp = freqB.CompareTo(freqA);
            if (cmp != 0)
            {
                return cmp;
            }

            cmp = b.Length.CompareTo(a.Length);
            return cmp != 0 ? cmp : string.Compare(a, b, StringComparison.Ordinal);
        });

        return results;
    }

    public static int GetDigitLength(string syllable)
    {
        return syllable?.Length ?? 0;
    }

    private static void SearchTrie(string digits, int pos, TrieNode node, string current, List<string> results)
    {
        if (node.IsEnd && current.Length > 0)
            results.Add(current);

        if (pos >= digits.Length || results.Count >= 20)
            return;

        int digit = digits[pos] - '0';
        if (digit < 2 || digit > 9) return;

        foreach (char ch in DigitLetters[digit])
        {
            if (node.Children.TryGetValue(ch, out var child))
                SearchTrie(digits, pos + 1, child, current + ch, results);
        }
    }

    private static TrieNode BuildTrie()
    {
        var root = new TrieNode();
        foreach (string s in AllSyllables)
        {
            var node = root;
            foreach (char c in s)
            {
                if (!node.Children.TryGetValue(c, out var child))
                {
                    child = new TrieNode();
                    node.Children[c] = child;
                }
                node = child;
            }
            node.IsEnd = true;
        }
        return root;
    }

    private sealed class TrieNode
    {
        public Dictionary<char, TrieNode> Children { get; } = new();
        public bool IsEnd { get; set; }
    }

    private static Dictionary<string, int> BuildFrequencyMap()
    {
        // 按汉语常用拼音频率排序，权重越高越常用
        string[] ranked =
        {
            "de", "shi", "yi", "bu", "le", "zai", "ren", "you", "wo", "ta",
            "zhe", "zhong", "da", "lai", "shang", "guo", "ge", "he", "dao", "shuo",
            "nian", "li", "hou", "jiu", "ye", "yao", "xia", "dui", "ke", "mei",
            "jing", "ba", "xue", "dian", "qi", "chu", "dong", "xing", "hui", "ran",
            "gong", "zi", "hao", "jin", "kai", "na", "xian", "zuo", "bian", "er",
            "ni", "men", "ma", "bei", "jia", "du", "fa", "dang", "zhi", "hai"
        };

        var freq = new Dictionary<string, int>(ranked.Length, StringComparer.Ordinal);
        for (int i = 0; i < ranked.Length; i++)
        {
            freq[ranked[i]] = ranked.Length - i;
        }
        return freq;
    }

    private static string[] GetAllSyllables()
    {
        return new[]
        {
            "a","ai","an","ang","ao",
            "ba","bai","ban","bang","bao","bei","ben","beng","bi","bian","biao","bie","bin","bing","bo","bu",
            "ca","cai","can","cang","cao","ce","cen","ceng","ci","cong","cou","cu","cuan","cui","cun","cuo",
            "cha","chai","chan","chang","chao","che","chen","cheng","chi","chong","chou","chu","chua","chuai","chuan","chuang","chui","chun","chuo",
            "da","dai","dan","dang","dao","de","dei","den","deng","di","dia","dian","diao","die","ding","diu","dong","dou","du","duan","dui","dun","duo",
            "e","ei","en","eng","er",
            "fa","fan","fang","fei","fen","feng","fo","fou","fu",
            "ga","gai","gan","gang","gao","ge","gei","gen","geng","gong","gou","gu","gua","guai","guan","guang","gui","gun","guo",
            "ha","hai","han","hang","hao","he","hei","hen","heng","hong","hou","hu","hua","huai","huan","huang","hui","hun","huo",
            "ji","jia","jian","jiang","jiao","jie","jin","jing","jiong","jiu","ju","juan","jue","jun",
            "ka","kai","kan","kang","kao","ke","kei","ken","keng","kong","kou","ku","kua","kuai","kuan","kuang","kui","kun","kuo",
            "la","lai","lan","lang","lao","le","lei","leng","li","lia","lian","liang","liao","lie","lin","ling","liu","lo","long","lou","lu","luan","lun","luo","lv","lve",
            "ma","mai","man","mang","mao","me","mei","men","meng","mi","mian","miao","mie","min","ming","miu","mo","mou","mu",
            "na","nai","nan","nang","nao","ne","nei","nen","neng","ni","nian","niang","niao","nie","nin","ning","niu","nong","nou","nu","nuan","nun","nuo","nv","nve",
            "o","ou",
            "pa","pai","pan","pang","pao","pei","pen","peng","pi","pian","piao","pie","pin","ping","po","pou","pu",
            "qi","qia","qian","qiang","qiao","qie","qin","qing","qiong","qiu","qu","quan","que","qun",
            "ran","rang","rao","re","ren","reng","ri","rong","rou","ru","rua","ruan","rui","run","ruo",
            "sa","sai","san","sang","sao","se","sen","seng","si","song","sou","su","suan","sui","sun","suo",
            "sha","shai","shan","shang","shao","she","shei","shen","sheng","shi","shou","shu","shua","shuai","shuan","shuang","shui","shun","shuo",
            "ta","tai","tan","tang","tao","te","teng","ti","tian","tiao","tie","ting","tong","tou","tu","tuan","tui","tun","tuo",
            "wa","wai","wan","wang","wei","wen","weng","wo","wu",
            "xi","xia","xian","xiang","xiao","xie","xin","xing","xiong","xiu","xu","xuan","xue","xun",
            "ya","yan","yang","yao","ye","yi","yin","ying","yo","yong","you","yu","yuan","yue","yun",
            "za","zai","zan","zang","zao","ze","zei","zen","zeng","zi","zong","zou","zu","zuan","zui","zun","zuo",
            "zha","zhai","zhan","zhang","zhao","zhe","zhei","zhen","zheng","zhi","zhong","zhou","zhu","zhua","zhuai","zhuan","zhuang","zhui","zhun","zhuo"
        };
    }
}
