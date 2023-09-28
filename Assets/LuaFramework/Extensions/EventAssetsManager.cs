using System;
using System.Collections.Generic;
using System.Text;

namespace Native
{
    public class EventAssetsManager
    {
        //! Update events code
        public enum EventCode
        {
            ERROR_NO_LOCAL_MANIFEST,
            ERROR_DOWNLOAD_MANIFEST,
            ERROR_PARSE_MANIFEST,
            NEW_VERSION_FOUND,
            ALREADY_UP_TO_DATE,
            UPDATE_PROGRESSION,
            ASSET_UPDATED,
            ERROR_UPDATING,
            UPDATE_FINISHED,
            UPDATE_FAILED,
            ERROR_DECOMPRESS
        }

        private EventCode _code;
        private AssetsManager _manager;
        private string _message;
        private string _assetId;
        private int _curle_code;
        private int _curlm_code;

        public EventAssetsManager(in string eventName, AssetsManager manager, in EventCode code, string assetId = "", string message = "", int curleCode = 0, int curlmCode = 0)
        {
            _code = code;
            _manager = manager;
            _message = message;
            _assetId = assetId;
            _curle_code = curleCode;
            _curlm_code = curlmCode;
        }


        public EventCode getEventCode() => _code;

        public int getCURLECode() => _curle_code;

        public int getCURLMCode() => _curlm_code;

        public string getMessage() => _message;

        public bool isResuming()
        {
            return true;
        }

        public float getPercent()
        {
            return 0;
        }

        public float getPercentByFile()
        {
            return 0;
        }

        double getDownloadedBytes()
        {
            return 0;
        }

        double getTotalBytes()
        {
            return 0;
        }

        int getDownloadedFiles()
        {
            return 0;
        }

        int getTotalFiles()
        {
            return 0;
        }
    }
}
