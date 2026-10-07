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
    'You cannot change your own role, permissions or account status.',
    'At least one active administrator with user management permission must remain.',
    'Student accounts cannot have staff permissions.',
    'A charge already exists for this accommodation and billing period.',
    'Accommodation event has not been received yet. Check the reference and retry after synchronization.',
    'Accommodation must belong to this student and must not be cancelled.',
    'A charge for this source already exists.',
    'Billing data changed concurrently. Retry using the same request ID.',
    'Charge changed concurrently. Retry using the same request ID.',
    'Payment data changed concurrently. Retry using the same request ID.',
    'This payment request or transaction reference has already been recorded.',
    'Charge not found.',
    'Payment not found.',
    'Payment cannot exceed the remaining charge amount.',
    'Request ID was already used for a different charge.',
    'Request ID was already used for a different payment.',
    'Card and bank payments require a unique transaction reference of at most 100 characters.',
    'Accommodation charges require a YYYY-MM billing period.',
    'Worker profile not found.',
    'Worker not found.',
    'Staff account not found.',
    'This account already has a worker profile.',
    'Reassign or resolve open tasks before deactivating this worker.',
    'Choose an active worker different from the current assignee.',
    'Only accepted or ongoing requests can be assigned.',
    'Only an assigned request can be started.',
    'Interventions and resolution require work in progress.',
    'Only the assigned worker or a maintenance supervisor can process this request.',
    'An inactive worker cannot process requests.',
    'Category not found.',
    'A category with this name already exists.',
    'Inactive categories cannot be used for new requests.',
    'Maintenance request not found.',
    'Closed requests cannot be changed.',
    'Only a submitted request can be accepted, rejected or cancelled.',
    'Meal entitlement not found.',
    'An entitlement already exists for this student, month and meal type.',
    'Allowed quantity must be positive and cannot be below the consumed quantity.',
    'Expired meal entitlement cannot be changed.',
    'Meal entitlement is not active for the current month.',
    'Meals cannot be consumed at an inactive restaurant.',
    'No meals remain for this entitlement.',
    'Purchases require an active, unexpired meal entitlement.',
    'Purchased quantity exceeds the supported entitlement limit.',
    'Request ID was already used for a different purchase.',
    'Request ID was already used for a different consumption.',
    'Restaurant not found.',
    'Menu not found.',
    'A menu already exists for this restaurant and date.',
    'Withdraw the published menu before editing it.',
    'Only menus of an active restaurant can be published.',
    'Menu is already published.',
    'Only a published menu can be withdrawn.',
    'Price must be between 0 and 99999999.99 with at most two decimal places.',
    'You do not have a current accommodation.',
    'Received accommodation eligibility was not found.',
    'Accommodation assignment was not found.',
    'The student already has an active accommodation.',
    'An inactive dorm cannot receive assignments.',
    'The room has no available bed.',
    'Only an assigned accommodation can be moved into once.',
    'Only a moved-in accommodation can be moved out of once.',
    'Only an assigned accommodation can be cancelled.',
    'Dorm was not found.',
    'Room was not found.',
    'Dorm capacity cannot be lower than the total room capacity.',
    'An occupied dorm cannot be deactivated.',
    'Activate the dorm before adding rooms.',
    'Activate the dorm before making a room available.',
    'Room number already exists in this dorm.',
    'The total room capacity would exceed dorm capacity.',
    'Capacity cannot be lower than current occupancy.',
    'An occupied room cannot be made inactive or put under maintenance.',
    'Inventory changed concurrently. Reload before retrying.',
    'The preliminary ranking has already been published.',
    'The final ranking has already been published.',
    'Ranking data has changed. Generate the draft again.',
    'Final ranking data changed. Generate the draft again.',
    'Close the competition before generating or publishing its ranking.',
    'There are no active submitted applications to rank.',
    'Publish a preliminary ranking first.',
    'Available places and the appeal deadline are required.',
    'Only a closed competition can be configured for conclusion.',
    'The initial appeal deadline must be in the future.',
    'An announced appeal deadline cannot be shortened.',
    'Configure the closed competition and wait for its appeal deadline before final ranking.',
    'All appeals must be resolved before final ranking.',
    'A tied group crosses the capacity boundary. Adjust capacity to include or exclude the entire group.',
    'Review documentation and recalculate current points after the appeal before accepting it.',
    'Only a submitted appeal can enter review.',
    'Only an appeal under review can be resolved.',
    'An appeal response of at most 4000 characters is required.',
    'Submitted application was not found.',
    'Only a submitted application can enter review.',
    'Start application review before reviewing documents.',
    'The application is no longer under review.',
    'Only an application under review can be scored.',
    'At least one document is required and every document must be VALID.',
    'A comment is required for an invalid document.',
    'Review comment must not exceed 2000 characters.',
    'All five scoring categories are required.',
    'Points must be non-negative, have at most two decimal places and fit decimal(18,2).',
    'Total points exceed the supported storage limit.',
    'The record has changed. Reload it before retrying.',
    'The review conflicted with another operation. Reload before retrying.',
    'The application has not been scored yet.',
    'Preliminary ranking was not found.',
    'Published preliminary ranking was not found.',
    'Final ranking was not found.',
    'Published final ranking was not found.',
    'Accommodation eligibility was not found.',
    'Appeal was not found.',
    'The appeal period is not open.',
    'Appeals can only be changed before the competition is finalized.',
    'Only applications on the preliminary ranking can be appealed.',
    'An appeal already exists for this application.',
    'Appeal reason must contain between 1 and 4000 characters.',
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
            (response.status === 409 &&
            typeof body?.message === 'string' &&
            /^Application [0-9a-f-]{36} requires completed document review and current scoring\.$/i.test(
                body.message,
            )
                ? body.message
                : '') ||
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
