using System;

namespace FOC.Application.Save
{
    public interface IAtomicSaveStore
    {
        SaveStoreResult Write(string slotName, string content, Func<string, bool> validateContent);

        SaveStoreResult Read(string slotName, Func<string, bool> validateContent);
    }
}

