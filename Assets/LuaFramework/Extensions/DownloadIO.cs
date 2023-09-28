using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Native
{

    public interface IDownloadIO
    {
        public bool init(in string filename, in string tempSuffix);

        public void finish();

        public void createHandler();
    }

    public class DownloadIO : IDownloadIO
    {
        static int _sSerialId = 0;

        // if more than one task write to one file, cause file broken
        // so use a set to check this situation
        static HashSet<string> _sStoragePathSet = new();

        public int serialId = 0;

        // header info
        public bool _acceptRanges = false;
        public bool _headerAchieved = false;
        public uint _totalBytesExpected = 0;

        string _header; // temp buffer for receive header string, only used in thread proc

        // progress
        public uint _bytesReceived = 0;
        public uint _fileSize = 0;
        public uint _totalBytesReceived = 0;

        // error
        public int _errCode = 0;
        public int _errCodeInternal = 0;
        public string _errDescription = string.Empty;

        // for saving data
        public string _fileName = string.Empty;
        public string _tempFileName = string.Empty;
        public MemoryStream _buf = null;

        public DownloadHandlerFile handler = null;

        public DownloadIO()
        {
            serialId = _sSerialId++;

            _initInternal();

            Debug.Log("Construct DownloadIO :" + serialId);
        }

        ~DownloadIO()
        {
            // if task destroyed unnormally, we should release WritenFileName stored in set.
            // Normally, this action should done when task finished.
            if (!string.IsNullOrEmpty(_tempFileName) && _sStoragePathSet.Contains(_tempFileName))
            {
                _sStoragePathSet.Remove(_tempFileName);
            }

            if (handler != null) { handler.Dispose(); }

            Debug.Log("Destruct DownloadIO :" + serialId);
        }

        public bool init(in string filename, in string tempSuffix)
        {
            if (string.IsNullOrEmpty(filename))
            {
                // data task
                _buf.Capacity = 16384;
                return true;
            }

            // file task
            _fileName = filename;
            _tempFileName = filename + tempSuffix;

            if (_sStoragePathSet.Contains(_tempFileName))
            {
                // there is another task uses this storage path
                _errCode = DownloadTask.ERROR_FILE_OP_FAILED;
                _errCodeInternal = 0;
                _errDescription = "More than one download file task write to same file:" + _tempFileName;
                return false;
            }

            _sStoragePathSet.Add(_tempFileName);

            // open temp file handle for write
            bool ret = false;
            do
            {
                string dir = Path.GetDirectoryName(_tempFileName);

                if (dir == null || dir == String.Empty)
                {
                    _errCode = DownloadTask.ERROR_INVALID_PARAMS;
                    _errCodeInternal = 0;
                    _errDescription = "Can't find dirname in storagePath.";
                    break;
                }

                if (!Directory.Exists(dir))
                {
                    if (!Directory.CreateDirectory(dir).Exists)
                    {
                        _errCode = DownloadTask.ERROR_FILE_OP_FAILED;
                        _errCodeInternal = 0;
                        _errDescription = "Can't create dir:" + dir;
                        break;
                    }
                }

                ret = true;
            } while (false);

            return ret;
        }

        public void createHandler()
        {
            // open file
            handler = new DownloadHandlerFile(_tempFileName, _acceptRanges);

            if (null == handler)
            {
                _errCode = DownloadTask.ERROR_FILE_OP_FAILED;
                _errCodeInternal = 0;
                _errDescription = "Can't open file:" + _tempFileName;
            }
        }

        void _initInternal()
        {
            _acceptRanges = (false);
            _headerAchieved = (false);
            _bytesReceived = (0);
            _fileSize = (0);
            _totalBytesReceived = (0);
            _totalBytesExpected = (0);
            _errCode = (DownloadTask.ERROR_NO_ERROR);
            _errCodeInternal = 0;
            //_header.resize(0);
            //_header.reserve(384); // pre alloc header string buffer
        }

        public void setErrorProc(int code, int codeInternal, in string desc) 
        {
            _errCode = code;
            _errCodeInternal = codeInternal;
            _errDescription = desc;
        }

        public void finish()
        {
            handler?.Dispose();

            do
            {
                if (0 == _fileName.Length)
                {
                    break;
                }

                // if file already exist, remove it
                if (File.Exists(_fileName))
                {
                    try {

                        if (_fileName != _tempFileName)
                        {
                            File.Delete(_fileName);
                            File.Move(_tempFileName, _fileName);
                        }

                        _sStoragePathSet.Remove(_tempFileName);
                        break;
                    } 
                    catch (IOException ex)
                    {
                        Debug.LogError(ex.Message);
                        _errCode = DownloadTask.ERROR_FILE_OP_FAILED;
                        _errCodeInternal = 0;
                        _errDescription = "Can't remove old file: " + _fileName;
                        break;
                    }
                }

                // failed
                _errCode = DownloadTask.ERROR_FILE_OP_FAILED;
                _errCodeInternal = 0;
                _errDescription = $"Can't rename file from: {_tempFileName} to: {_fileName}";
            } while (false);
        }
    }
}
