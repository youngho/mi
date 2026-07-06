namespace PinkSoft.MissionSDK
{
    public static class MissionSDKVersion
    {
        public const string Current = "1.0.0";

        public static bool IsCompatible(string missionVersion)
        {
            if (string.IsNullOrEmpty(missionVersion))
                return false;
            var coreMajor = Current.Split('.')[0];
            var missionMajor = missionVersion.Split('.')[0];
            return coreMajor == missionMajor;
        }
    }
}
