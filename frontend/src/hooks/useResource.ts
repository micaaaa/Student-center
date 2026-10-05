import { useEffect, useState } from 'react';
import { api } from '../lib/api';

export function useResource<T>(path: string) {
    const [state, setState] = useState<{
        path: string;
        data: T | null;
        error: unknown;
        loading: boolean;
    }>({
        path,
        data: null,
        error: null,
        loading: true,
    });
    const [version, setVersion] = useState(0);
    useEffect(() => {
        const controller = new AbortController();
        setState({ path, data: null, error: null, loading: true });
        api<T>(path, { signal: controller.signal })
            .then((data) => {
                if (!controller.signal.aborted)
                    setState({ path, data, error: null, loading: false });
            })
            .catch((error) => {
                if (!controller.signal.aborted)
                    setState({ path, data: null, error, loading: false });
            });
        return () => controller.abort();
    }, [path, version]);
    return {
        ...(state.path === path ? state : { data: null, error: null, loading: true }),
        reload: () => setVersion((value) => value + 1),
        setData: (data: T) => setState({ path, data, error: null, loading: false }),
    };
}
