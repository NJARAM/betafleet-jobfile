export type JobRow = {
  id: string; customer: string; customerId: number; reference: string; cargo: string; cargoType: string; direction: string;
  units: string[]; stage: string; progressPct: number; owner: string; openedOn: string; eta: string; alert: string;
};
export type Handoff = { id: number; unit: string; jobId: string; fromUnit: string; title: string; note: string; state: string; acceptedBy?: string; createdAt: string; acceptedAt?: string };
export type Leg = { unit: string; number?: string; status?: string; progressPct?: number; owner?: string; summary?: string; off?: boolean };
export type TimelineEvent = { id: number; date: string; unit: string; title: string; detail: string; done: boolean };
export type Charge = { id: number; unit: string; description: string; kind: 'Revenue' | 'Disbursement'; revenue: number; cost: number; status: string; invoiceNo?: string };
export type Expense = {
  id: string; jobId: string; unit: string; type: string; payee: string; amount: number; currency: string; reference: string; rechargeable: boolean;
  notes: string; status: string; paymentVoucher?: string; requestedBy: string; date: string;
};
export type Shipment = {
  id: string; jobId: string; fillingMasterId?: string; truck: string; driver: string; routeCode: string; progressPct: number; place: string;
  speedKmh: number; eta: string; status: string; gpsKm: number; standardKm: number; deliveredAt?: string; podSignedBy?: string;
};
export type ContainerRow = {
  id: number; jobId: string; clearingId: string; number: string; size: string; status: string; dischargedOn: string; returnedOn?: string; depot?: string;
  daysOut: number; freeDays: number; detentionDays: number; detentionState: string; events: { date: string; text: string }[];
};
export type Activity = { id: number; at: string; actor: string; text: string };
export type FitCheck = { key: string; label: string; ok: boolean; detail: string };
export type Lookups = {
  units: Record<string, string>; customers: { id: number; name: string }[]; routes: { code: string; name: string }[];
  vehicles: { reg: string; type: string; status: string; subcontracted: boolean }[]; drivers: string[]; trailers: string[];
  warehouses: string[]; entryTypes: string[]; depots: string[]; containerSizes: string[]; expenseTypes: Record<string, string[]>;
  clearingStages: string[]; requestSteps: string[];
};
