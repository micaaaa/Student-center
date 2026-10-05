export type Role = 'STUDENT' | 'STAFF' | 'ADMIN';

export interface User {
    id: string;
    username: string;
    email: string;
    role: Role;
    status: string;
    permissions: string[];
}

export interface Session {
    accessToken: string;
    refreshToken: string;
    expiresAtUtc: string;
    user: User;
}

export interface Student {
    id: string;
    userId: string;
    studentNumber: string;
    firstName: string;
    lastName: string;
    email: string;
    phone: string | null;
    faculty: string | null;
    studyProgram: string | null;
    studyLevel: string | null;
    yearOfStudy: number | null;
    fundingType: string | null;
    address: string | null;
    status: string;
}
