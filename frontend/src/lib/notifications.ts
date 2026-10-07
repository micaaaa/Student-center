import type { User } from './types';

export interface Notification {
    id: string;
    eventType: string;
    title: string;
    message: string;
    resourceType: string;
    resourceId: string;
    occurredAtUtc: string;
    readAtUtc: string | null;
}

export function notificationTarget(
    item: Notification,
    user: User,
): { to: string; label: string } | null {
    const id = encodeURIComponent(item.resourceId);
    if (user.role === 'STUDENT') {
        switch (item.resourceType) {
            case 'Application':
                return { to: '/applications/' + id, label: 'View application' };
            case 'ApplicationResults':
                return {
                    to: '/applications/' + id + '/results',
                    label: 'View application results',
                };
            case 'Accommodation':
                return { to: '/my-accommodation', label: 'View accommodation and history' };
            case 'MealPurchase':
                return { to: '/my-meals', label: 'View meals and purchases' };
            case 'Payment':
                return { to: '/my-billing', label: 'View payments' };
            case 'Charge':
                return { to: '/my-billing/charges/' + id, label: 'View charge' };
            case 'MaintenanceRequest':
                return { to: '/maintenance/' + id, label: 'View repair request' };
        }
    } else if (item.resourceType === 'MaintenanceRequest') {
        return {
            to:
                (user.permissions.includes('ManageMaintenance')
                    ? '/staff/maintenance/'
                    : '/maintenance-work/') + id,
            label: 'View repair request',
        };
    }
    return null;
}

export function notificationsChanged() {
    window.dispatchEvent(new Event('notifications-changed'));
}
