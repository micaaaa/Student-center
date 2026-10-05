import { ApiError } from './api';
import { useResource } from '../hooks/useResource';

export interface Score {
    academicPoints: number;
    incomePoints: number;
    ectsPoints: number;
    studyYearPoints: number;
    additionalPoints: number;
    totalPoints: number;
    calculatedAtUtc: string;
    isCurrent: boolean;
}

export interface Ranking {
    tieRule: string;
    publishedAtUtc: string | null;
    availablePlaces?: number;
    entries: {
        applicationId: string;
        position: number;
        totalPoints: number;
        isMine: boolean;
        eligible?: boolean;
    }[];
}

export interface ConclusionSettings {
    availablePlaces: number | null;
    appealDeadlineUtc: string | null;
    status: string;
}

export interface Eligibility {
    eligible: boolean;
    academicYear: string;
    decisionDateUtc: string;
}

export interface Appeal {
    id: string;
    reason: string;
    status: string;
    submittedAtUtc: string;
    response: string | null;
    resolvedAtUtc: string | null;
}

// Only explicit domain absence is an empty state; other failures remain retryable errors.
export function useOptionalResource<T>(path: string, absentMessages: string[]) {
    const resource = useResource<T>(path);
    const absent =
        resource.error instanceof ApiError &&
        resource.error.status === 404 &&
        absentMessages.includes(resource.error.message);
    return { ...resource, error: absent ? null : resource.error, absent };
}
