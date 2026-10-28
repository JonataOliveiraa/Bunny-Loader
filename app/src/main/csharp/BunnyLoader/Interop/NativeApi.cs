using System;
using System.Runtime.InteropServices;

namespace BunnyLoader.Interop;

// O espelho da tabela de app/src/main/cpp/script/dotnet/NativeApi.cpp: mesma
// ordem, mesmo layout. Campo novo entra no fim dos dois lados.
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct NativeApi
{
    public uint Size;
    public delegate* unmanaged<int, byte*, void> Log;
    public delegate* unmanaged<byte*, byte*, IntPtr> FindClass;
    public delegate* unmanaged<IntPtr, byte*, int> FieldOffset;
    public delegate* unmanaged<IntPtr, byte*, int, IntPtr> FindMethod;
    public delegate* unmanaged<IntPtr, IntPtr, IntPtr*> InstallHook;
    public delegate* unmanaged<IntPtr, IntPtr> MethodPointer;
    public delegate* unmanaged<IntPtr, IntPtr> NewObject;
    public delegate* unmanaged<IntPtr, byte*, IntPtr> FindNestedClass;
    public delegate* unmanaged<IntPtr, byte*, IntPtr> FindMethodBySignature;
    public delegate* unmanaged<IntPtr, int*, int, int> CheckLayout;
    public delegate* unmanaged<IntPtr, CallFrame*, int> CallGame;
    public delegate* unmanaged<byte*> LastCallError;
    public delegate* unmanaged<IntPtr, byte*, IntPtr> FindField;
    public delegate* unmanaged<IntPtr, IntPtr> StaticFieldAddress;
    public delegate* unmanaged<IntPtr, void*, void> StaticFieldGet;
    public delegate* unmanaged<IntPtr, void*, void> StaticFieldSet;
    public delegate* unmanaged<char*, int, IntPtr> NewString;
    public delegate* unmanaged<IntPtr, int> ClassValueSize;
    public delegate* unmanaged<IntPtr, IntPtr, int> IsInstance;
    public delegate* unmanaged<IntPtr, int, uint> GcHandleNew;
    public delegate* unmanaged<uint, IntPtr> GcHandleTarget;
    public delegate* unmanaged<uint, void> GcHandleFree;
    public delegate* unmanaged<IntPtr, int, IntPtr> NewArray;
    public delegate* unmanaged<IntPtr, byte*> ClassName;
    public delegate* unmanaged<IntPtr, IntPtr, IntPtr> VirtualMethod;
    public delegate* unmanaged<byte*, byte*, byte*, byte*, byte*, int> RegisterItem;
    public delegate* unmanaged<IntPtr, int, void> PrepareItem;
    public delegate* unmanaged<IntPtr, int, void> FinishItem;
    public delegate* unmanaged<delegate* unmanaged<int, int, void>, void> OnItemsInstalled;
    public delegate* unmanaged<delegate* unmanaged<void>, void> OnContentReady;
    public delegate* unmanaged<int> IsGameThread;
    public delegate* unmanaged<byte*, byte*, IntPtr> RequestModTexture;
    public delegate* unmanaged<byte*, int*, IntPtr> RequestVanillaTexture;
    public delegate* unmanaged<IntPtr, IntPtr> TextureAssetValue;
}
