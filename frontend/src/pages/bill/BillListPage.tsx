import * as React from 'react';
import { Box, Button, Stack, Typography, IconButton, Paper } from '@mui/material';
import { DataGrid } from '@mui/x-data-grid';
import type { GridColDef, GridRenderCellParams } from '@mui/x-data-grid';
import AddIcon from '@mui/icons-material/Add';
import EditIcon from '@mui/icons-material/Edit';
import DeleteIcon from '@mui/icons-material/Delete';
// 模拟账单数据
const rows = [
  { id: 1, name: '水费', amount: 156.5, date: '2025-01-05', status: '已支付' },
  { id: 2, name: '电费', amount: 230.2, date: '2025-01-10', status: '未支付' },
  { id: 3, name: '物业', amount: 98.3,  date: '2025-01-11', status: '已支付' },
];

// 列定义
const columns: GridColDef[] = [
  { field: 'id', headerName: '账单ID', width: 90 },
  { field: 'name', headerName: '账单名称', width: 180 },
  { field: 'amount', headerName: '金额（元）', width: 120 },
  { field: 'date', headerName: '日期', width: 140 },
  { field: 'status', headerName: '状态', width: 120 },
  {
    field: 'actions',
    headerName: '操作',
    width: 120,
    sortable: false,
    renderCell: (params: GridRenderCellParams) => (
      <Stack direction="row" spacing={1}>
        <IconButton color="primary" size="small" onClick={() => handleEdit(params.row)}>
          <EditIcon fontSize="inherit" />
        </IconButton>
        <IconButton color="error" size="small" onClick={() => handleDelete(params.row)}>
          <DeleteIcon fontSize="inherit" />
        </IconButton>
      </Stack>
    ),
  },
];

// 这里放操作函数，可以替换为实际业务实现
const handleEdit = (row: any) => {
  alert(`编辑账单：${row.name}`);
};
const handleDelete = (row: any) => {
  alert(`删除账单：${row.name}`);
};

export default function BillListPage() {
  return (
    <Box sx={{ p: 3 }}>
      <Typography variant="h5" gutterBottom>
        账单列表
      </Typography>
      <Paper elevation={1} sx={{ p: 2 }}>
        <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ mb: 2 }}>
          <Typography variant="h6">账单明细</Typography>
          <Button variant="contained" startIcon={<AddIcon />}>
            新增账单
          </Button>
        </Stack>
        <DataGrid
          rows={rows}
          columns={columns}
          autoHeight
          disableRowSelectionOnClick
          initialState={{
            pagination: { paginationModel: { page: 0, pageSize: 5 } }
          }}
          pageSizeOptions={[5, 10, 20]}
        />
      </Paper>
    </Box>
  );
}