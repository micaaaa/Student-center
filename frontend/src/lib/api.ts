import type { Session, User } from './types';

const baseUrl = (import.meta.env.VITE_API_BASE_URL || 'https://localhost:50386').replace(/\/$/, '');
const storageKey = 'student-center.session';
const listeners = new Set<() => void>();
let session: Session | null = readSession();
let revision = 0;
let refreshTask: Promise<void> | null = null;

function readSession(): Session | null {
    try {
        const value = JSON.parse(sessionStorage.getItem(storageKey) || 'null');
        if (
            value &&
            typeof value.accessToken === 'string' &&
            typeof value.refreshToken === 'string' &&
            typeof value.expiresAtUtc === 'string' &&
            value.user?.id &&
            ['STUDENT', 'STAFF', 'ADMIN'].includes(value.user.role)
        ) {
            return value;
        }
    } catch {
        // An unavailable or malformed browser store must not prevent login.
    }
    return null;
}

export function getSession() {
    return session;
}

export function subscribe(listener: () => void) {
    listeners.add(listener);
    return () => {
        listeners.delete(listener);
    };
}

function saveSession(value: Session | null) {
    session = value;
    try {
        if (value) sessionStorage.setItem(storageKey, JSON.stringify(value));
        else sessionStorage.removeItem(storageKey);
    } catch {
        // Keep the session in memory if browser storage is unavailable.
    }
    listeners.forEach((listener) => listener());
}

export class ApiError extends Error {
    constructor(
        public status: number,
        message: string,
    ) {
        super(message);
    }
}

const translations: Record<string, string> = {
    'Username is already in use.': 'This username is already in use.',
    'Email is already in use.': 'An account with this email address already exists.',
    'Student number is already in use.': 'A profile with this student number already exists.',
    'A student profile already exists for this user.':
        'A student profile already exists. Refresh the page.',
};

const applicationMessages = new Set([
    'Create a student profile before managing applications.',
    'You already have an application for this competition.',
    'Applications are allowed only for an open competition.',
    'The competition application period is not active.',
    'Only a draft application can be edited.',
    'Only a draft application can be submitted.',
    'Documents can only be changed on a draft application.',
    'Application was not found.',
    'Competition was not found.',
    'Document was not found.',
    'Document file was not found.',
    'Allowed file formats are PDF, JPG and PNG.',
    'File content does not match the selected format.',
    'The file is empty.',
    'The maximum file size is 10 MB.',
]);

interface RequestOptions extends RequestInit {
    responseType?: 'json' | 'blob';
}

async function send<T>(path: string, options: RequestOptions = {}, token?: string): Promise<T> {
    const headers = new Headers(options.headers);
    if (options.body && !(options.body instanceof FormData)) {
        headers.set('Content-Type', 'application/json');
    }
    if (token) headers.set('Authorization', 'Bearer ' + token);

    let response: Response;
    try {
        const timeout = AbortSignal.timeout(20_000);
        const signal = options.signal ? AbortSignal.any([options.signal, timeout]) : timeout;
        const { responseType: _, ...requestOptions } = options;
        response = await fetch(baseUrl + path, { ...requestOptions, headers, signal });
    } catch (error) {
        if (error instanceof DOMException && error.name === 'AbortError') throw error;
        throw new ApiError(0, 'Unable to connect to the server. Please try again.');
    }
    if (!response.ok) {
        const body = await response.json().catch(() => null);
        const message =
            translations[body?.message] ||
            (response.status < 500 && applicationMessages.has(body?.message) ? body.message : '') ||
            (response.status === 401
                ? 'Authentication failed. Check your credentials or sign in again.'
                : response.status === 403
                  ? 'You do not have permission to perform this action.'
                  : response.status >= 500
                    ? 'The service is temporarily unavailable. Please try again.'
                    : response.status === 400
                      ? 'Check the information provided and try again.'
                      : response.status === 413
                        ? 'The maximum file size is 10 MB.'
                        : response.status === 404
                          ? 'The requested record was not found.'
                          : response.status === 409
                            ? 'The submitted information conflicts with an existing record.'
                            : 'The request could not be completed. Please try again.');
        throw new ApiError(response.status, message);
    }
    if (response.status === 204) return undefined as T;
    return options.responseType === 'blob' ? (response.blob() as Promise<T>) : response.json();
}

async function refresh() {
    if (refreshTask) return refreshTask;
    const current = session;
    const startedAt = revision;
    if (!current) throw new ApiError(401, 'Please sign in again.');
    refreshTask = (async () => {
        try {
            const next = await send<Session>('/api/auth/refresh', {
                method: 'POST',
                body: JSON.stringify({ refreshToken: current.refreshToken }),
            });
            // A pending refresh must never restore a session after logout or another login.
            if (startedAt === revision) saveSession(next);
        } catch (error) {
            if (startedAt === revision && error instanceof ApiError && error.status === 401) {
                revision++;
                saveSession(null);
            }
            throw error;
        }
    })().finally(() => {
        refreshTask = null;
    });
    return refreshTask;
}

export async function api<T>(path: string, options: RequestOptions = {}): Promise<T> {
    const requestRevision = revision;
    if (!session) throw new ApiError(401, 'Please sign in again.');
    if (Date.parse(session.expiresAtUtc) <= Date.now() + 30_000) await refresh();
    if (!session || requestRevision !== revision)
        throw new ApiError(401, 'Your session has ended.');
    const token = session.accessToken;
    try {
        return await send<T>(path, options, token);
    } catch (error) {
        if (!(error instanceof ApiError) || error.status !== 401) throw error;
        if (!session || requestRevision !== revision) throw error;
        if (session.accessToken === token) await refresh();
        if (!session || requestRevision !== revision) throw error;
        try {
            return await send<T>(path, options, session.accessToken);
        } catch (retryError) {
            if (
                retryError instanceof ApiError &&
                retryError.status === 401 &&
                requestRevision === revision
            ) {
                revision++;
                saveSession(null);
            }
            throw retryError;
        }
    }
}

export async function authenticate(kind: 'login' | 'register', body: object) {
    const startedAt = ++revision;
    const next = await send<Session>('/api/auth/' + kind, {
        method: 'POST',
        body: JSON.stringify(body),
    });
    if (startedAt === revision) saveSession(next);
}

export async function validateSession() {
    if (!session) return;
    const startedAt = revision;
    const user = await api<User>('/api/auth/me');
    if (session && startedAt === revision) saveSession({ ...session, user });
}

export async function logout() {
    // Finish a rotating refresh before revoking its latest token.
    const pending = refreshTask;
    if (pending) await pending.catch(() => {});
    const token = session?.refreshToken;
    revision++;
    saveSession(null);
    if (token)
        await send('/api/auth/logout', {
            method: 'POST',
            body: JSON.stringify({ refreshToken: token }),
        });
}

export function errorMessage(error: unknown) {
    return error instanceof Error ? error.message : 'An error occurred. Please try again.';
}
