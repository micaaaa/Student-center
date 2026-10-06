import { useState } from 'react';
import { Link, useNavigate } from 'react-router';
import { useResource } from '../hooks/useResource';
import type { Restaurant } from '../lib/food';
import { RequestError, StatusBadge } from '../components/ApplicationUi';
import { RestaurantForm } from '../components/FoodForms';

export function RestaurantsPage({ management = false }: { management?: boolean }) {
    const resource = useResource<Restaurant[]>(
        '/api/restaurants' + (management ? '/management' : ''),
    );
    const [adding, setAdding] = useState(false);
    const [search, setSearch] = useState('');
    const navigate = useNavigate();
    const base = management ? '/staff/restaurants/' : '/restaurants/';
    const visible = resource.data?.filter((item) =>
        `${item.name} ${item.address}`.toLowerCase().includes(search.trim().toLowerCase()),
    );
    return (
        <>
            <div className="page-heading heading-row">
                <div>
                    <span className="eyebrow">DINING SERVICES</span>
                    <h1>Restaurants and menus</h1>
                </div>
                {management && (
                    <button className="primary" disabled={adding} onClick={() => setAdding(true)}>
                        Add restaurant
                    </button>
                )}
            </div>
            {adding && (
                <RestaurantForm
                    cancel={() => setAdding(false)}
                    saved={(item) => navigate(base + item.id)}
                />
            )}
            <div className="list-toolbar">
                <label>
                    Search restaurants
                    <input
                        type="search"
                        value={search}
                        onChange={(e) => setSearch(e.target.value)}
                    />
                </label>
            </div>
            {resource.loading ? (
                <p role="status">Loading restaurants…</p>
            ) : resource.error ? (
                <RequestError error={resource.error} retry={resource.reload} />
            ) : !visible?.length ? (
                <section className="panel">No restaurants found.</section>
            ) : (
                <div className="competition-list">
                    {visible.map((item) => (
                        <article className="panel competition-card" key={item.id}>
                            <StatusBadge status={item.status === 1 ? 'ACTIVE' : 'INACTIVE'} />
                            <h2>{item.name}</h2>
                            <p>{item.address}</p>
                            <Link className="secondary" to={base + item.id}>
                                {management ? 'Manage restaurant and menus' : 'View menus'}
                            </Link>
                        </article>
                    ))}
                </div>
            )}
        </>
    );
}
