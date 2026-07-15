using System;
using System.Collections.Generic;

public interface IContainerEntrySource<TEnum> where TEnum : struct
{
    event Action OnSourceChanged;

    List<(TEnum key, int count)> GetEntries();
}
