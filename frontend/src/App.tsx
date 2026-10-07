import { NotificationsPage } from './pages/NotificationsPage';
import { StudentsPage, StudentPage } from './pages/StudentsPage';
import { BrowserRouter, Link, Route, Routes } from 'react-router';
import { AuthProvider, ProtectedRoute } from './auth/AuthContext';
import { Layout } from './components/Layout';
import { AuthPage } from './pages/AuthPage';
import { Dashboard } from './pages/Dashboard';
import { ProfilePage } from './pages/ProfilePage';
import { CompetitionsPage } from './pages/CompetitionsPage';
import { CompetitionPage } from './pages/CompetitionPage';
import { ApplicationsPage } from './pages/ApplicationsPage';
import { ApplicationPage } from './pages/ApplicationPage';
import { ApplicationResultsPage } from './pages/ApplicationResultsPage';
import { RankingsPage } from './pages/RankingsPage';
import { StaffApplicationsPage } from './pages/StaffApplicationsPage';
import { StaffApplicationPage } from './pages/StaffApplicationPage';
import { StaffRankingsPage } from './pages/StaffRankingsPage';
import { StaffCompetitionResultsPage } from './pages/StaffCompetitionResultsPage';
import { StaffCompetitionsPage } from './pages/StaffCompetitionsPage';
import { NewCompetitionPage, StaffCompetitionPage } from './pages/StaffCompetitionPage';
import { DormsPage } from './pages/DormsPage';
import { DormPage } from './pages/DormPage';
import { AssignmentsPage } from './pages/AssignmentsPage';
import { AssignmentPage } from './pages/AssignmentPage';
import { MyAccommodationPage } from './pages/MyAccommodationPage';
import { RestaurantsPage } from './pages/RestaurantsPage';
import { RestaurantPage } from './pages/RestaurantPage';
import { MealsPage } from './pages/MealsPage';
import { MaintenanceCategoriesPage } from './pages/MaintenanceCategoriesPage';
import { MaintenanceRequestsPage } from './pages/MaintenanceRequestsPage';
import { MaintenanceRequestPage } from './pages/MaintenanceRequestPage';
import { NewMaintenancePage } from './pages/NewMaintenancePage';
import { MaintenanceWorkersPage } from './pages/MaintenanceWorkersPage';
import { MaintenanceTasksPage, MaintenanceTaskPage } from './pages/MaintenanceTasksPage';
import { BillingPage } from './pages/BillingPage';
import { ChargePage } from './pages/ChargePage';

export default function App() {
    return (
        <BrowserRouter>
            <AuthProvider>
                <Routes>
                    <Route path="/login" element={<AuthPage key="login" />} />
                    <Route path="/register" element={<AuthPage key="register" register />} />
                    <Route element={<ProtectedRoute />}>
                        <Route element={<Layout />}>
                            <Route index element={<Dashboard />} />
                            <Route path="/notifications" element={<NotificationsPage />} />
                            <Route
                                element={
                                    <ProtectedRoute
                                        roles={['STAFF', 'ADMIN']}
                                        permission="ManageBilling"
                                    />
                                }
                            >
                                <Route path="/staff/billing" element={<BillingPage management />} />
                                <Route
                                    path="/staff/billing/charges/:id"
                                    element={<ChargePage management />}
                                />
                            </Route>
                            <Route element={<ProtectedRoute roles={['STAFF', 'ADMIN']} />}>
                                <Route path="/staff/students" element={<StudentsPage />} />
                                <Route path="/staff/students/:id" element={<StudentPage />} />
                                <Route
                                    path="/maintenance-work"
                                    element={<MaintenanceTasksPage />}
                                />
                                <Route
                                    path="/maintenance-work/:id"
                                    element={<MaintenanceTaskPage />}
                                />
                            </Route>
                            <Route
                                element={
                                    <ProtectedRoute
                                        roles={['STAFF', 'ADMIN']}
                                        permission="ManageMaintenance"
                                    />
                                }
                            >
                                <Route
                                    path="/staff/maintenance"
                                    element={<MaintenanceRequestsPage management />}
                                />
                                <Route
                                    path="/staff/maintenance/workers"
                                    element={<MaintenanceWorkersPage />}
                                />
                                <Route
                                    path="/staff/maintenance/categories"
                                    element={<MaintenanceCategoriesPage />}
                                />
                                <Route
                                    path="/staff/maintenance/:id"
                                    element={<MaintenanceRequestPage management />}
                                />
                            </Route>
                            <Route
                                element={
                                    <ProtectedRoute
                                        roles={['STAFF', 'ADMIN']}
                                        permission="ManageFood"
                                    />
                                }
                            >
                                <Route
                                    path="/staff/restaurants"
                                    element={<RestaurantsPage management />}
                                />
                                <Route path="/staff/meals" element={<MealsPage management />} />
                                <Route
                                    path="/staff/restaurants/:id"
                                    element={<RestaurantPage management />}
                                />
                            </Route>
                            <Route
                                element={
                                    <ProtectedRoute
                                        roles={['STAFF', 'ADMIN']}
                                        permission="ManageAccommodation"
                                    />
                                }
                            >
                                <Route path="/staff/dorms" element={<DormsPage />} />
                                <Route path="/staff/dorms/:id" element={<DormPage />} />
                                <Route path="/staff/assignments" element={<AssignmentsPage />} />
                                <Route
                                    path="/staff/accommodations/:id"
                                    element={<AssignmentPage />}
                                />
                            </Route>
                            <Route
                                element={
                                    <ProtectedRoute
                                        roles={['STAFF', 'ADMIN']}
                                        permission="ManageApplications"
                                    />
                                }
                            >
                                <Route
                                    path="/staff/applications"
                                    element={<StaffApplicationsPage />}
                                />
                                <Route
                                    path="/staff/competitions"
                                    element={<StaffCompetitionsPage />}
                                />
                                <Route
                                    path="/staff/competitions/new"
                                    element={<NewCompetitionPage />}
                                />
                                <Route
                                    path="/staff/competitions/:id"
                                    element={<StaffCompetitionPage />}
                                />
                                <Route path="/staff/rankings" element={<StaffRankingsPage />} />
                                <Route
                                    path="/staff/rankings/:id"
                                    element={<StaffCompetitionResultsPage />}
                                />
                                <Route
                                    path="/staff/applications/:id"
                                    element={<StaffApplicationPage />}
                                />
                            </Route>
                            <Route element={<ProtectedRoute roles={['STUDENT']} />}>
                                <Route path="/my-billing" element={<BillingPage />} />
                                <Route path="/my-billing/charges/:id" element={<ChargePage />} />
                                <Route path="/maintenance" element={<MaintenanceRequestsPage />} />
                                <Route path="/maintenance/new" element={<NewMaintenancePage />} />
                                <Route
                                    path="/maintenance/:id"
                                    element={<MaintenanceRequestPage />}
                                />
                                <Route path="/my-meals" element={<MealsPage />} />
                                <Route path="/restaurants" element={<RestaurantsPage />} />
                                <Route path="/restaurants/:id" element={<RestaurantPage />} />
                                <Route path="/my-accommodation" element={<MyAccommodationPage />} />
                                <Route path="/profile" element={<ProfilePage />} />
                                <Route path="/competitions" element={<CompetitionsPage />} />
                                <Route path="/competitions/:id" element={<CompetitionPage />} />
                                <Route
                                    path="/competitions/:id/rankings"
                                    element={<RankingsPage />}
                                />
                                <Route path="/applications" element={<ApplicationsPage />} />
                                <Route path="/applications/:id" element={<ApplicationPage />} />
                                <Route
                                    path="/applications/:id/results"
                                    element={<ApplicationResultsPage />}
                                />
                            </Route>
                            <Route
                                path="/access-denied"
                                element={
                                    <div className="panel">
                                        <h1>Access denied</h1>
                                        <Link to="/">Return to overview</Link>
                                    </div>
                                }
                            />
                        </Route>
                    </Route>
                    <Route
                        path="*"
                        element={
                            <div className="screen-state">
                                <h1>Page not found</h1>
                                <Link to="/">Return to home</Link>
                            </div>
                        }
                    />
                </Routes>
            </AuthProvider>
        </BrowserRouter>
    );
}
