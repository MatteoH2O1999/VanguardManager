// Copyright (C) 2026 Matteo Dell'Acqua
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY, without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
// See the
// GNU Affero General Public License for more details.
//
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

namespace Manager.Vanguard.Common
{
    /// <summary>
    /// Lock types that can be acquired with <see cref="Lock.Acquire(Locks)"/> or
    /// <see cref="Lock.TryAcquire(Locks)"/>.
    /// </summary>
    public enum Locks
    {
        UPDATER,
        SERVICE,
        LAUNCHER,
    }

    /// <summary>
    /// Static class to handle exclusive access to parts of this product.
    /// </summary>
    public static class Lock
    {
        private static string ToMutexName(this Locks lockType) =>
            lockType switch
            {
                Locks.UPDATER => $"Global\\{ApplicationData.AppName}_UPDATER",
                Locks.SERVICE => $"Global\\{ApplicationData.AppName}_SERVICE",
                Locks.LAUNCHER => $"Global\\{ApplicationData.AppName}_LAUNCHER",
                _ => throw new InvalidLockTypeException(lockType),
            };

        /// <summary>
        /// Acquires the specified <see cref="Locks"/>.
        /// </summary>
        /// <param name="lockType">The <see cref="Locks"/> to acquire.</param>
        /// <returns>The <see cref="IDisposable"/> that holds the lock.</returns>
        /// <exception cref="LockException">If the lock could not be acquired.</exception>
        public static IDisposable Acquire(this Locks lockType)
        {
            string mutexName = lockType.ToMutexName();
            Mutex mutex;
            bool created;
            try
            {
                mutex = new(true, mutexName, out created);
            }
            catch (Exception ex)
            {
                throw new LockException(mutexName, ex);
            }
            if (!created)
            {
                try
                {
                    mutex.WaitOne();
                }
                catch (Exception ex)
                {
                    throw new LockException(mutexName, ex);
                }
                finally
                {
                    mutex.Dispose();
                }
            }
            return new LockManager(mutex);
        }

        /// <summary>
        /// Tries to acquire the specified <see cref="Locks"/>.
        /// </summary>
        /// <param name="lockType">The <see cref="Locks"/> to acquire.</param>
        /// <returns>
        /// The <see cref="IDisposable"/> that holds the lock or <see langword="null"/> if the
        /// lock is already in use.
        /// </returns>
        /// <exception cref="LockException">If the lock could not be acquired.</exception>
        public static IDisposable? TryAcquire(this Locks lockType)
        {
            string mutexName = lockType.ToMutexName();
            Mutex mutex;
            bool created;
            try
            {
                mutex = new(true, mutexName, out created);
            }
            catch (Exception ex)
            {
                throw new LockException(mutexName, ex);
            }
            if (!created)
            {
                mutex.Dispose();
                return null;
            }
            return new LockManager(mutex);
        }

        private sealed class LockManager(Mutex m) : IDisposable
        {
            private readonly Mutex mutex = m;
            private bool disposed;

            ~LockManager()
            {
                this.Dispose();
            }

            public void Dispose()
            {
                if (!this.disposed)
                {
                    this.mutex.ReleaseMutex();
                    this.mutex.Dispose();
                    this.disposed = true;
                }
                GC.SuppressFinalize(this);
            }
        }
    }

    file sealed class InvalidLockTypeException(Locks lockType) : Exception($"Invalid lock type: {lockType}");

    /// <summary>
    /// Represents errors that occur while acquiring a <see cref="Mutex"/>.
    /// </summary>
    /// <param name="mutexName">The name of the mutex</param>
    /// <param name="ex">The <see cref="Exception"/></param>
    public sealed class LockException(string mutexName, Exception ex)
        : Exception($"Could not acquire mutex {mutexName}", ex);
}
