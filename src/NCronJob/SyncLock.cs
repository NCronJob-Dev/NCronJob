#if NET9_0_OR_GREATER
global using SyncLock = System.Threading.Lock;
#else
global using SyncLock = System.Object;
#endif
