#include <wdm.h>
#include <d3dkmddi.h>

//
// Minimal WDDM Miniport Driver Skeleton for Windows 7
//

#define VIRTUAL_DISPLAY_TAG 'VDis'

// Forward declarations
NTSTATUS DriverEntry(IN PDRIVER_OBJECT DriverObject, IN PUNICODE_STRING RegistryPath);
NTSTATUS HybAddDevice(IN PDRIVER_OBJECT DriverObject, IN PDEVICE_OBJECT PhysicalDeviceObject);
VOID HybUnload(IN PDRIVER_OBJECT DriverObject);

// DxgkDdi functions
NTSTATUS HybDdiAddDevice(IN CONST PDEVICE_OBJECT PhysicalDeviceObject, OUT PVOID* MiniportDeviceContext);
NTSTATUS HybDdiRemoveDevice(IN CONST PVOID MiniportDeviceContext);
NTSTATUS HybDdiStartDevice(IN CONST PVOID MiniportDeviceContext, IN DXGK_START_INFO* DxgkStartInfo, IN DXGKRNL_INTERFACE* DxgkInterface, OUT ULONG* NumberOfVideoPresentSources, OUT ULONG* NumberOfChildren);
NTSTATUS HybDdiStopDevice(IN CONST PVOID MiniportDeviceContext);
NTSTATUS HybDdiDispatchIoRequest(IN CONST PVOID MiniportDeviceContext, IN ULONG VidPnSourceId, IN PVIDEO_REQUEST_PACKET VideoRequestPacket);

//
// Driver Entry Point
//
NTSTATUS DriverEntry(IN PDRIVER_OBJECT DriverObject, IN PUNICODE_STRING RegistryPath)
{
    DRIVER_INITIALIZATION_DATA DriverInitializationData = { 0 };

    DriverInitializationData.Version = DXGKDDI_INTERFACE_VERSION_VISTA; // Win7 supports Vista+ WDDM
    DriverInitializationData.DxgkDdiAddDevice = HybDdiAddDevice;
    DriverInitializationData.DxgkDdiRemoveDevice = HybDdiRemoveDevice;
    DriverInitializationData.DxgkDdiStartDevice = HybDdiStartDevice;
    DriverInitializationData.DxgkDdiStopDevice = HybDdiStopDevice;
    DriverInitializationData.DxgkDdiDispatchIoRequest = HybDdiDispatchIoRequest;
    // ... other required DDIs must be implemented ...

    return DxgkInitialize(DriverObject, RegistryPath, &DriverInitializationData);
}

NTSTATUS HybDdiAddDevice(IN CONST PDEVICE_OBJECT PhysicalDeviceObject, OUT PVOID* MiniportDeviceContext)
{
    // Allocate context
    *MiniportDeviceContext = ExAllocatePoolWithTag(NonPagedPool, sizeof(ULONG), VIRTUAL_DISPLAY_TAG);
    if (*MiniportDeviceContext == NULL)
    {
        return STATUS_INSUFFICIENT_RESOURCES;
    }
    RtlZeroMemory(*MiniportDeviceContext, sizeof(ULONG));
    return STATUS_SUCCESS;
}

NTSTATUS HybDdiRemoveDevice(IN CONST PVOID MiniportDeviceContext)
{
    if (MiniportDeviceContext)
    {
        ExFreePool(MiniportDeviceContext);
    }
    return STATUS_SUCCESS;
}

NTSTATUS HybDdiStartDevice(IN CONST PVOID MiniportDeviceContext, IN DXGK_START_INFO* DxgkStartInfo, IN DXGKRNL_INTERFACE* DxgkInterface, OUT ULONG* NumberOfVideoPresentSources, OUT ULONG* NumberOfChildren)
{
    // Simulate 1 monitor
    *NumberOfVideoPresentSources = 1;
    *NumberOfChildren = 1;
    return STATUS_SUCCESS;
}

NTSTATUS HybDdiStopDevice(IN CONST PVOID MiniportDeviceContext)
{
    return STATUS_SUCCESS;
}

NTSTATUS HybDdiDispatchIoRequest(IN CONST PVOID MiniportDeviceContext, IN ULONG VidPnSourceId, IN PVIDEO_REQUEST_PACKET VideoRequestPacket)
{
    // Handle IOCTLs from User Mode App here
    return STATUS_SUCCESS;
}
