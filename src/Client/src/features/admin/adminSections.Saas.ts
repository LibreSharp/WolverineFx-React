// The back office's sections: the tabs on every /admin page (AdminLayout).
export const adminSections = [
  { to: '/admin', label: 'Dashboard', end: true },
  { to: '/admin/organisations', label: 'Organisations' },
  { to: '/admin/users', label: 'Users' },
  { to: '/admin/features', label: 'Feature flags' },
  { to: '/admin/audit', label: 'Audit log' },
  { to: '/admin/failed-messages', label: 'Failed messages' },
]
