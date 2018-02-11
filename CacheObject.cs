namespace csharp_misc
{
    using System;
    using System.ComponentModel;

    /// <summary>
    /// Encapsulates a value and updates that value if it has expired.
    /// </summary>
    public class CacheObject<T> :
        INotifyPropertyChanged,
        IDisposable
    {
        private Boolean _isDisposed;
        private DateTime? _lastUpdate;
        private TimeSpan? _timeToLive;
        private Boolean _notifyValueChanged;
        private Func<T> _updater;
        private Action<Object, UpdateErrorEventArgs> _updateErrorCallback;
        private T _initialValue;
        private T _value;

        /// <summary>
        /// Initializes a new instance of CacheObject with the specified value.
        /// This constructor takes no updater, so the value can be considered to be constant.
        /// </summary>
        /// <param name="value">The current and initial value.</param>
        public CacheObject(T value)
            : this(value, value, null, null, true, null, null, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of CacheObject with the specified updater and time to live.
        /// As no initial value is given, this CacheObject can be considered to be invalid and will be updated as soon as the value is requested.
        /// </summary>
        /// <param name="updater">A function to call when the value has expired and needs to be updated.</param>
        /// <param name="timeToLive">The time span the encapsulated value is considered to be valid before it has to be updated.</param>
        public CacheObject(Func<T> updater, TimeSpan timeToLive)
            : this(default(T), default(T), DateTime.MinValue, timeToLive, true, updater, null, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of CacheObject with the specified value, updater and time to live.
        /// </summary>
        /// <param name="value">The current and initial value.</param>
        /// <param name="updater">A function to call when the value has expired and needs to be updated.</param>
        /// <param name="timeToLive">The time span the encapsulated value is considered to be valid before it has to be updated.</param>
        public CacheObject(T value, Func<T> updater, TimeSpan timeToLive)
            : this(value, value, DateTime.Now, timeToLive, true, updater, null, null)
        {
        }

        private CacheObject(T currentValue, T initialValue, DateTime? lastUpdate, TimeSpan? timeToLive, Boolean notifyValueChanged, Func<T> updater, Action<Object, UpdateErrorEventArgs> updateErrorCallback, UpdateErrorEventHandler updateErrorEventHandler)
        {
            _isDisposed = false;
            _lastUpdate = lastUpdate;
            _timeToLive = timeToLive;
            _notifyValueChanged = notifyValueChanged;
            _updater = updater;
            _updateErrorCallback = updateErrorCallback;
            _initialValue = initialValue;
            _value = currentValue;

            if (updateErrorEventHandler != null)
            {
                UpdateError += updateErrorEventHandler;
            }
        }

        /// <summary>
        /// An event raised when an error updating the value occurs.
        /// </summary>
        public event UpdateErrorEventHandler UpdateError;

        /// <summary>
        /// An event raised when the value has changed.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Determines whether the value has expired and must be updated.
        /// </summary>
        public Boolean HasExpired
        {
            get { return _lastUpdate == null || _timeToLive == null ? false : _lastUpdate.Value + _timeToLive.Value < DateTime.Now; }
        }

        /// <summary>
        /// Gets the initial value.
        /// </summary>
        public T InitialValue
        {
            get { return _initialValue; }
        }

        /// <summary>
        /// Determines whether the encapsulated value has been disposed.
        /// </summary>
        public Boolean IsDisposed
        {
            get { return _isDisposed; }
        }

        /// <summary>
        /// Gets or sets the timestamp of the last update.
        /// </summary>
        public DateTime? LastUpdate
        {
            get { return _lastUpdate; }
            set
            {
                CheckAccess();

                _lastUpdate = value;
            }
        }

        /// <summary>
        /// Gets or sets the time span the value is valid before it has to be updated.
        /// </summary>
        public TimeSpan? TimeToLive
        {
            get { return _timeToLive; }
            set
            {
                CheckAccess();

                _timeToLive = value;
            }
        }

        /// <summary>
        /// Specifies whether the NotifyPropertChanged event should be raised if the encapsulated value has changed.
        /// </summary>
        public Boolean NotifyValueChanged
        {
            get { return _notifyValueChanged; }
            set
            {
                CheckAccess();

                _notifyValueChanged = value;
            }
        }

        /// <summary>
        /// Gets or sets the callback to be called when an exception occurred while updating the encapsulated value.
        /// </summary>
        public Action<Object, UpdateErrorEventArgs> UpdateErrorCallback
        {
            get { return _updateErrorCallback; }
            set
            {
                CheckAccess();

                _updateErrorCallback = value;
            }
        }

        /// <summary>
        /// Gets or sets the encapsulated value and performs an update if necessary.
        /// </summary>
        public T Value
        {
            get
            {
                Update();

                return _value;
            }
            set
            {
                CheckAccess();

                _value = value;
                _lastUpdate = DateTime.Now;

                if (_notifyValueChanged)
                {
                    RaiseValueChanged();
                }
            }
        }

        /// <summary>
        /// Disposes the encapsulated value (if necessary).
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Gets the current value without updating it.
        /// </summary>
        /// <returns></returns>
        public T GetValue()
        {
            CheckAccess();

            return _value;
        }

        /// <summary>
        /// Sets the current value without modifying the update timestamp and without notifying of changes.
        /// </summary>
        /// <param name="value"></param>
        public void SetValue(T value)
        {
            CheckAccess();

            _value = value;
        }

        /// <summary>
        /// Resets the value to its initial value and and sets the update timestamp.
        /// </summary>
        public void Reset()
        {
            CheckAccess();

            _value = _initialValue;
            _lastUpdate = _lastUpdate ?? DateTime.Now;
        }

        /// <summary>
        /// Performs an update to the value if necessary.
        /// </summary>
        public void Update()
        {
            CheckAccess();

            if (HasExpired)
            {
                try
                {
                    if (_updater != null)
                    {
                        Value = _updater.Invoke();
                    }
                }
                catch (Exception ex)
                {
                    if (UpdateErrorCallback == null && UpdateError == null)
                    {
                        throw;
                    }
                    else
                    {
                        UpdateErrorCallback?.Invoke(this, new UpdateErrorEventArgs(ex));
                        UpdateError?.Invoke(this, new UpdateErrorEventArgs(ex));
                    }
                }
            }
        }

        /// <summary>
        /// Determines whether the encapsulated value of this object and the encapsulated value of the other object are equal - or if both CacheObjects are considered equal.
        /// </summary>
        public override Boolean Equals(Object obj)
        {
            if (_value == null)
            {
                var other = obj as CacheObject<T>;

                if (other == null)
                {
                    return false;
                }

                return Object.Equals(this, other);
            }
            else
            {
                return _value.Equals(obj);
            }
        }

        /// <summary>
        /// Returns the hash code for the encapsulated value or the hash code for this CacheObject if the value is null.
        /// </summary>
        /// <returns></returns>
        public override Int32 GetHashCode()
        {
            if (_value == null)
            {
                return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
            }
            else
            {
                return _value.GetHashCode();
            }
        }

        /// <summary>
        /// Returns the string representation of the encapsulated value or the string representation of this CacheObject.
        /// </summary>
        /// <returns></returns>
        public override String ToString()
        {
            return _value?.ToString();
        }

        /// <summary>
        /// Implicitly casts the given CacheObject to the type of the encapsulated value.
        /// </summary>
        /// <param name="obj"></param>
        public static implicit operator T(CacheObject<T> obj)
        {
            return obj.Value;
        }

        /// <summary>
        /// Checks if this CacheObject has already been disposed.
        /// </summary>
        private void CheckAccess()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(CacheObject<T>));
            }
        }

        /// <summary>
        /// Disposes the encapsulated value.
        /// </summary>
        /// <param name="disposing"></param>
        private void Dispose(Boolean disposing)
        {
            if (_isDisposed == false)
            {
                if (disposing)
                {
                    (_value as IDisposable)?.Dispose();
                }

                _isDisposed = true;
            }
        }

        /// <summary>
        /// Raises the PropertyChanged event.
        /// </summary>
        private void RaiseValueChanged()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
        }
    }
    
    public delegate void UpdateErrorEventHandler(Object sender, UpdateErrorEventArgs e);

    public class UpdateErrorEventArgs :
        EventArgs
    {
        public UpdateErrorEventArgs(Exception exception)
        {
            Exception = exception;
        }

        public Exception Exception { get; }
    }
}
