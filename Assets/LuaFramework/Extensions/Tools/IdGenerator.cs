using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Extension
{
    public class IdGenerator
    {
        private List<int> _idIdle = new() { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 };
        private List<int> _idActive = new();
        private int _burthen = 15;

        public int burthen
        {
            get {
                return _burthen;    
            }
        }

        public int generateId()
        {
            int id = -1;

            if (this._idIdle.Count > 0)
            {
                id = _idIdle[0];
                _idIdle.RemoveAt(0);
                _idActive.Add(id);

                return id;
            }

            id = this._burthen++;
            _idActive.Add(id);
            return id;
        }

        public void recycleId(int id)
        {
            int index = _idActive.IndexOf(id);
            if (index == -1)
            {
                Debug.LogWarning("找不到id:" + id);
                return;
            }
            _idActive.RemoveAt(index);
            _idIdle.Add(id);
        }

        public void dump()
        {
            Debug.Log("Idle: " + string.Join("\t", _idIdle));
            Debug.Log("Active: " + string.Join("\t", _idActive));
        }
    }
}
