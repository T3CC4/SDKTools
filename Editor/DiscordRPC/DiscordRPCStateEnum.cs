namespace SDKTools
{
    public static class RpcStateInfo
    {
        public static string StateName(this RpcState state)
        {
            switch (state)
            {
                case RpcState.EDITMODE: return "in Edit mode";
                case RpcState.PLAYMODE: return "in Play mode";
                case RpcState.UPLOADAVATAR: return "Uploading 3.0 Avatar";
                default: return "Error";
            }
        }
    }

        public enum RpcState
    {
        EDITMODE = 0,
        PLAYMODE = 1,
        UPLOADAVATAR = 2
    }
}