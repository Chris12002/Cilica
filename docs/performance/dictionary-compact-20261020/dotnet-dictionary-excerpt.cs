	internal ref TValue FindValue(TKey key)
	{
		if (key == null)
		{
			ThrowHelper.ThrowArgumentNullException(ExceptionArgument.key);
		}
		ref Entry reference = ref Unsafe.NullRef<Entry>();
		if (_buckets != null)
		{
			IEqualityComparer<TKey> comparer = _comparer;
			if (typeof(TKey).IsValueType && comparer == null)
			{
				uint hashCode = (uint)EqualityComparer<TKey>.Default.GetHashCode(key);
				int bucket = GetBucket(hashCode);
				Entry[] entries = _entries;
				uint num = 0u;
				bucket--;
				while ((uint)bucket < (uint)entries.Length)
				{
					reference = ref entries[bucket];
					if (reference.hashCode != hashCode || !EqualityComparer<TKey>.Default.Equals(reference.key, key))
					{
						bucket = reference.next;
						num++;
						if (num <= (uint)entries.Length)
						{
							continue;
						}
						goto IL_0113;
					}
					goto IL_0118;
				}
			}
			else
			{
				uint hashCode2 = (uint)comparer.GetHashCode(key);
				int bucket2 = GetBucket(hashCode2);
				Entry[] entries2 = _entries;
				uint num2 = 0u;
				bucket2--;
				while ((uint)bucket2 < (uint)entries2.Length)
				{
					reference = ref entries2[bucket2];
					if (reference.hashCode != hashCode2 || !comparer.Equals(reference.key, key))
					{
						bucket2 = reference.next;
						num2++;
						if (num2 <= (uint)entries2.Length)
						{
							continue;
						}
						goto IL_0113;
					}
					goto IL_0118;
				}
			}
		}
		return ref Unsafe.NullRef<TValue>();
		IL_0113:
		ThrowHelper.ThrowInvalidOperationException_ConcurrentOperationsNotSupported();
		goto IL_0118;
		IL_0118:
		return ref reference.value;
	}

	private int Initialize(int capacity)
	{
		int prime = HashHelpers.GetPrime(capacity);
		int[] buckets = new int[prime];
		Entry[] entries = new Entry[prime];
		_freeList = -1;
		_fastModMultiplier = HashHelpers.GetFastModMultiplier((uint)prime);
		_buckets = buckets;
		_entries = entries;
		return prime;
	}

	private bool TryInsert(TKey key, TValue value, InsertionBehavior behavior)
	{
		if (key == null)
		{
			ThrowHelper.ThrowArgumentNullException(ExceptionArgument.key);
		}
		if (_buckets == null)
		{
			Initialize(0);
		}
		Entry[] entries = _entries;
		IEqualityComparer<TKey> comparer = _comparer;
		uint num = (uint)((typeof(TKey).IsValueType && comparer == null) ? key.GetHashCode() : comparer.GetHashCode(key));
		uint num2 = 0u;
		ref int bucket = ref GetBucket(num);
		int num3 = bucket - 1;
		if (typeof(TKey).IsValueType && comparer == null)
		{
			while ((uint)num3 < (uint)entries.Length)
			{
				if (entries[num3].hashCode == num && EqualityComparer<TKey>.Default.Equals(entries[num3].key, key))
				{
					switch (behavior)
					{
					case InsertionBehavior.OverwriteExisting:
						entries[num3].value = value;
						return true;
					case InsertionBehavior.ThrowOnExisting:
						ThrowHelper.ThrowAddingDuplicateWithKeyArgumentException(key);
						break;
					}
					return false;
				}
				num3 = entries[num3].next;
				num2++;
				if (num2 > (uint)entries.Length)
				{
					ThrowHelper.ThrowInvalidOperationException_ConcurrentOperationsNotSupported();
				}
			}
		}
		else
		{
			while ((uint)num3 < (uint)entries.Length)
			{
				if (entries[num3].hashCode == num && comparer.Equals(entries[num3].key, key))
				{
					switch (behavior)
					{
					case InsertionBehavior.OverwriteExisting:
						entries[num3].value = value;
						return true;
					case InsertionBehavior.ThrowOnExisting:
						ThrowHelper.ThrowAddingDuplicateWithKeyArgumentException(key);
						break;
					}
					return false;
				}
				num3 = entries[num3].next;
				num2++;
				if (num2 > (uint)entries.Length)
				{
					ThrowHelper.ThrowInvalidOperationException_ConcurrentOperationsNotSupported();
				}
			}
		}
		int num4;
		if (_freeCount > 0)
		{
			num4 = _freeList;
			_freeList = -3 - entries[_freeList].next;
			_freeCount--;
		}
		else
		{
			int count = _count;
			if (count == entries.Length)
			{
				Resize();
				bucket = ref GetBucket(num);
			}
			num4 = count;
			_count = count + 1;
			entries = _entries;
		}
		ref Entry reference = ref entries[num4];
		reference.hashCode = num;
		reference.next = bucket - 1;
		reference.key = key;
		reference.value = value;
		bucket = num4 + 1;
		_version++;
		if (!typeof(TKey).IsValueType && num2 > 100 && comparer is NonRandomizedStringEqualityComparer)
		{
			Resize(entries.Length, forceNewHashCodes: true);
		}
		return true;
	}

	public AlternateLookup<TAlternateKey> GetAlternateLookup<TAlternateKey>() where TAlternateKey : notnull
	{
		if (!AlternateLookup<TAlternateKey>.IsCompatibleKey(this))
		{
			ThrowHelper.ThrowInvalidOperationException(ExceptionResource.InvalidOperation_IncompatibleComparer);
		}
		return new AlternateLookup<TAlternateKey>(this);
	}

	public bool TryGetAlternateLookup<TAlternateKey>(out AlternateLookup<TAlternateKey> lookup) where TAlternateKey : notnull
	{
		if (AlternateLookup<TAlternateKey>.IsCompatibleKey(this))
		{
			lookup = new AlternateLookup<TAlternateKey>(this);
			return true;
		}
		lookup = default(AlternateLookup<TAlternateKey>);
		return false;
	}

	public virtual void OnDeserialization(object? sender)
	{
		HashHelpers.SerializationInfoTable.TryGetValue(this, out var value);
		if (value == null)
		{
			return;
		}
		int @int = value.GetInt32("Version");
		int int2 = value.GetInt32("HashSize");
		_comparer = (IEqualityComparer<TKey>)value.GetValue("Comparer", typeof(IEqualityComparer<TKey>));
		if (int2 != 0)
		{
			Initialize(int2);
			KeyValuePair<TKey, TValue>[] array = (KeyValuePair<TKey, TValue>[])value.GetValue("KeyValuePairs", typeof(KeyValuePair<TKey, TValue>[]));
			if (array == null)
			{
				ThrowHelper.ThrowSerializationException(ExceptionResource.Serialization_MissingKeys);
			}
			for (int i = 0; i < array.Length; i++)
			{
				if (array[i].Key == null)
				{
					ThrowHelper.ThrowSerializationException(ExceptionResource.Serialization_NullKey);
				}
				Add(array[i].Key, array[i].Value);
			}
		}
