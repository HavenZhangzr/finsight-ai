import * as React from 'react';
import { DataGrid } from '@mui/x-data-grid';
import type { GridColDef } from '@mui/x-data-grid';

const columns: GridColDef[] = [
  { field: 'id', headerName: 'ID', width: 90 },
  { field: 'name', headerName: '账单名称', width: 180 },
  { field: 'amount', headerName: '金额', width: 120 },
  { field: 'date', headerName: '日期', width: 140 },
  // 可以继续扩展其他字段...
];

const rows = [
  { id: 1, name: '水费', amount: 156.5, date: '2025-01-05' },
  { id: 2, name: '电费', amount: 230.2, date: '2025-01-10' },
  { id: 3, name: '电话费', amount: 98.3, date: '2025-01-11' },
];

export default function CustomizedDataGrid() {
  return (
    <div style={{ height: 400, width: '100%' }}>
      <DataGrid
        rows={rows}
        columns={columns}
        initialState={{
            pagination: {
            paginationModel: { pageSize: 5, page: 0 },
            },
        }}
        pageSizeOptions={[5]}
        />
    </div>
  );
}