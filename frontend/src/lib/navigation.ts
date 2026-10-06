import { BedDouble, ClipboardList, Utensils, Wallet, Wrench, type LucideIcon } from 'lucide-react';
import type { User } from './types';

interface ServiceLink {
    label: string;
    to: string;
}

export interface PortalService {
    id: string;
    title: string;
    description: string;
    icon: LucideIcon;
    links: ServiceLink[];
}

export function portalServices(user: User): PortalService[] {
    if (user.role === 'STUDENT') {
        return [
            {
                id: 'applications',
                title: 'Apply for accommodation',
                icon: ClipboardList,
                description:
                    'Find an open competition, submit your application and follow the results.',
                links: [
                    { label: 'View competitions', to: '/competitions' },
                    { label: 'My applications', to: '/applications' },
                ],
            },
            {
                id: 'housing',
                title: 'My room',
                icon: BedDouble,
                description: 'View your room assignment and accommodation history.',
                links: [{ label: 'View my accommodation', to: '/my-accommodation' }],
            },
            {
                id: 'meals',
                title: 'Meals and menus',
                icon: Utensils,
                description: 'Check your remaining meals, purchase history and restaurant menus.',
                links: [
                    { label: 'View my meals', to: '/my-meals' },
                    { label: 'Restaurant menus', to: '/restaurants' },
                ],
            },
            {
                id: 'payments',
                title: 'Charges and payments',
                icon: Wallet,
                description: 'Check what you owe and review recorded payments.',
                links: [{ label: 'View my balance', to: '/my-billing' }],
            },
            {
                id: 'maintenance',
                title: 'Room repairs',
                icon: Wrench,
                description: 'Report a problem in your room and track the repair.',
                links: [
                    { label: 'My repair requests', to: '/maintenance' },
                    { label: 'Report a problem', to: '/maintenance/new' },
                ],
            },
        ];
    }

    const services: PortalService[] = [
        {
            id: 'students',
            title: 'Students',
            icon: ClipboardList,
            description: 'Find a student by name or student number and open their record.',
            links: [{ label: 'Find a student', to: '/staff/students' }],
        },
    ];
    if (user.permissions.includes('ManageApplications')) {
        services.push({
            id: 'applications',
            title: 'Admissions',
            icon: ClipboardList,
            description: 'Manage competitions, review applications and publish rankings.',
            links: [
                { label: 'Competitions', to: '/staff/competitions' },
                { label: 'Review applications', to: '/staff/applications' },
                { label: 'Rankings and appeals', to: '/staff/rankings' },
            ],
        });
    }
    if (user.permissions.includes('ManageAccommodation')) {
        services.push({
            id: 'housing',
            title: 'Accommodation',
            icon: BedDouble,
            description: 'Assign rooms and manage student arrivals and departures.',
            links: [
                { label: 'Room assignments', to: '/staff/assignments' },
                { label: 'Dormitories and rooms', to: '/staff/dorms' },
            ],
        });
    }
    if (user.permissions.includes('ManageFood')) {
        services.push({
            id: 'meals',
            title: 'Dining',
            icon: Utensils,
            description: 'Record meal purchases and consumption, and manage menus.',
            links: [
                { label: 'Student meals', to: '/staff/meals' },
                { label: 'Restaurants and menus', to: '/staff/restaurants' },
            ],
        });
    }
    if (user.permissions.includes('ManageBilling')) {
        services.push({
            id: 'payments',
            title: 'Billing',
            icon: Wallet,
            description: 'Review student balances, create charges and record received payments.',
            links: [{ label: 'Charges and payments', to: '/staff/billing' }],
        });
    }
    services.push({
        id: 'maintenance',
        title: 'Maintenance',
        icon: Wrench,
        description: user.permissions.includes('ManageMaintenance')
            ? 'Review repair requests, assign workers and track interventions.'
            : 'View your assigned repairs and record interventions.',
        links: [
            ...(user.permissions.includes('ManageMaintenance')
                ? [
                      { label: 'Repair requests', to: '/staff/maintenance' },
                      { label: 'Workers', to: '/staff/maintenance/workers' },
                      { label: 'Repair categories', to: '/staff/maintenance/categories' },
                  ]
                : []),
            { label: 'My assigned repairs', to: '/maintenance-work' },
        ],
    });
    return services;
}

export function serviceLinkActive(pathname: string, to: string, services: PortalService[]) {
    const matches = services
        .flatMap((service) => service.links)
        .filter((link) => pathname === link.to || pathname.startsWith(link.to + '/'));
    const closest = matches.sort((a, b) => b.to.length - a.to.length)[0];
    // Assignment details have their own route outside the assignments list.
    if (pathname.startsWith('/staff/accommodations/')) return to === '/staff/assignments';
    return closest?.to === to;
}
