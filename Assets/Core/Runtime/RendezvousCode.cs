namespace PinkSoft.Core
{
    /// <summary>
    /// 접선용 5자 집결 코드 (표시·무전). 내부 partyId(UUID)와 분리된다.
    /// 문자셋은 O/0·I/1/L 혼동을 피한 Crockford 계열.
    /// </summary>
    public static class RendezvousCode
    {
        public const int Length = 5;
        const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        public static string Generate()
        {
            var chars = new char[Length];
            for (var i = 0; i < Length; i++)
                chars[i] = Alphabet[UnityEngine.Random.Range(0, Alphabet.Length)];
            return new string(chars);
        }

        public static bool IsValid(string? code)
        {
            if (string.IsNullOrEmpty(code) || code.Length != Length)
                return false;
            for (var i = 0; i < code.Length; i++)
            {
                if (Alphabet.IndexOf(char.ToUpperInvariant(code[i])) < 0)
                    return false;
            }
            return true;
        }

        public static string Normalize(string code) => code.Trim().ToUpperInvariant();

        /// <summary>무전/TTS용 한국어 음성 표기. 예: "알파, 탱고, 쓰리, 에코, 찰리. 집결하라."</summary>
        public static string ToRadioPhrase(string code)
        {
            code = Normalize(code);
            var parts = new string[code.Length];
            for (var i = 0; i < code.Length; i++)
                parts[i] = ToPhonetic(code[i]);
            return string.Join(", ", parts) + ". 집결하라.";
        }

        /// <summary>화면용 짧은 표기. 예: "알파 · 탱고 · 쓰리 · 에코 · 찰리"</summary>
        public static string ToPhoneticLine(string code)
        {
            code = Normalize(code);
            var parts = new string[code.Length];
            for (var i = 0; i < code.Length; i++)
                parts[i] = ToPhonetic(code[i]);
            return string.Join(" · ", parts);
        }

        public static string ToPhonetic(char c)
        {
            return char.ToUpperInvariant(c) switch
            {
                'A' => "알파",
                'B' => "브라보",
                'C' => "찰리",
                'D' => "델타",
                'E' => "에코",
                'F' => "폭스트롯",
                'G' => "골프",
                'H' => "호텔",
                'J' => "줄리엣",
                'K' => "킬로",
                'L' => "리마",
                'M' => "마이크",
                'N' => "노벰버",
                'P' => "파파",
                'Q' => "퀘벡",
                'R' => "로미오",
                'S' => "시에라",
                'T' => "탱고",
                'U' => "유니폼",
                'V' => "빅터",
                'W' => "위스키",
                'X' => "엑스레이",
                'Y' => "양키",
                'Z' => "줄루",
                '2' => "투",
                '3' => "쓰리",
                '4' => "포",
                '5' => "파이브",
                '6' => "식스",
                '7' => "세븐",
                '8' => "에잇",
                '9' => "나인",
                _ => c.ToString()
            };
        }
    }
}
