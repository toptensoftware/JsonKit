// JsonKit v0.5 - A simple but flexible Json library in a single .cs file.
// 
// Copyright (C) 2014 Topten Software (contact@toptensoftware.com) All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this product 
// except in compliance with the License. You may obtain a copy of the License at
// 
// http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software distributed under the 
// License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, 
// either express or implied. See the License for the specific language governing permissions 
// and limitations under the License.

using System;
using System.Collections.Generic;
using System.Threading;


namespace Topten.JsonKit
{
    class ThreadSafeCache<TKey, TValue>
    {
        public ThreadSafeCache()
        {

        }

        public TValue Get(TKey key, Func<TValue> createIt)
        {
            // Check if already exists
            _lock.EnterReadLock();
            try
            {
                TValue val;
                if (_map.TryGetValue(key, out val))
                    return val;
            }
            finally
            {
                _lock.ExitReadLock();
            }

            // Nope, take lock and try again
            _lock.EnterWriteLock();
            try
            {
                // Check again before creating it
                TValue val;
                if (!_map.TryGetValue(key, out val))
                {
					// Store the new one
					val = createIt();
                    _map[key] = val;
                }
                return val;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public bool TryGetValue(TKey key, out TValue val)
        {
            _lock.EnterReadLock();
            try
            {
                return _map.TryGetValue(key, out val);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public void Set(TKey key, TValue value)
        {
            _lock.EnterWriteLock();
            try
            {
                _map[key] = value;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        Dictionary<TKey, TValue> _map = new Dictionary<TKey,TValue>();
        ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();
    }
}
