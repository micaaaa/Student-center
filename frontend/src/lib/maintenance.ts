export interface MaintenanceCategory {
    id: string;
    name: string;
    description: string | null;
    isActive: boolean;
}
export interface MaintenanceRequest {
    id: string;
    studentId: string;
    accommodationId: string;
    roomId: string;
    categoryId: string;
    title: string;
    description: string;
    priority: number;
    status: number;
    createdAtUtc: string;
    updatedAtUtc: string;
    reviewedAtUtc: string | null;
    rejectionReason: string | null;
    cancelledAtUtc: string | null;
    assignedWorkerId: string | null;
    assignedAtUtc: string | null;
    startedAtUtc: string | null;
    resolvedAtUtc: string | null;
    resolutionDescription: string | null;
}
export const requestStatuses = [
    'SUBMITTED',
    'ACCEPTED',
    'REJECTED',
    'CANCELLED',
    'ASSIGNED',
    'IN_PROGRESS',
    'RESOLVED',
];
export const priorities = ['Low', 'Medium', 'High', 'Urgent'];
