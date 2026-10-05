import type { Appeal } from './results';

export interface StaffRanking {
    id: string;
    status: string;
    tieRule: string;
    availablePlaces?: number;
    publishedAtUtc: string | null;
    entries: {
        applicationId: string;
        studentId: string;
        position: number;
        totalPoints: number;
        eligible?: boolean;
    }[];
}

export interface StaffAppeal extends Appeal {
    applicationId: string;
    studentId: string;
}
