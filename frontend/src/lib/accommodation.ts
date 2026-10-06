export interface Dorm {
    id: string;
    name: string;
    address: string;
    city: string;
    category: string;
    capacity: number;
    status: string;
}

export interface Room {
    id: string;
    dormId: string;
    roomNumber: string;
    floor: number;
    capacity: number;
    occupiedBeds: number;
    status: string;
}
