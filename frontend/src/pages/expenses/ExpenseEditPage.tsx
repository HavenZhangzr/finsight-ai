import React, { useState, useEffect } from "react";
import type { SelectChangeEvent } from "@mui/material/Select";
import dayjs, { Dayjs } from "dayjs";
import {
    Box,
    Button,
    TextField,
    MenuItem,
    Select,
    InputLabel,
    FormControl,
    Typography,
    Stack,
    Paper,
    Popover,
} from "@mui/material";
import { DatePicker } from "@mui/x-date-pickers/DatePicker";
import UploadFileIcon from "@mui/icons-material/UploadFile";
import AutoAwesomeIcon from '@mui/icons-material/AutoAwesome';
import Tooltip from '@mui/material/Tooltip';
import IconButton from '@mui/material/IconButton';
import { useParams } from "react-router-dom"; // 用于获取URL参数
import Snackbar from '@mui/material/Snackbar';
import Alert from '@mui/material/Alert';
import InfoOutlinedIcon from '@mui/icons-material/InfoOutlined';
import WarningAmberOutlinedIcon from '@mui/icons-material/WarningAmberOutlined';

type ExpenseType = "Expense" | "Bill" | "Check" | "PurchaseOrder";

interface ExpenseEditProps {
    initialValues?: Partial<ExpenseFormData>;
    onSave?: (data: ExpenseFormData) => void;
}

export interface ExpenseFormData {
    date?: Dayjs | null;
    type: ExpenseType;
    payee: string;
    category: string;
    total?: string;
    description: string;
    files?: File[];
}

const payeeOptions = [
    "Bob's Burger Joint",
    "Squeaky Kleen Car Wash",
    "Pam Seitz",
    "Tania's Nursery",
    "Hicks Hardware",
];

const categoryOptions = [
    "Automobile",
    "Checking",
    "Decks and Patios",
    "Meals and Entertainment",
    "Plants and Soil",
    "Legal & Professional Fees",
    "Fuel",
    "Accounting",
    "Job Expenses",
    "Advertising",
    "Equipment Rental",
    "Fountain and Garden Lighting",
    "Equipment Repairs",
    "Sprinklers and Drip Systems",
    "Office Expenses",
    "Insurance",
    "Miscellaneous",
    "Maintenance and Repair",
    "Lawyer",
    "Gas and Electric",
    "Rent or Lease",
    "Telephone",
    "Bookkeeper",
    "--Split--"
];

const typeOptions: ExpenseType[] = [
    "Expense",
    "Bill",
    "Check",
    "PurchaseOrder",
];

// 你可以放在函数组件等合适位置
const aiDisclaimer = (
    <Alert
        severity="info"
        icon={<InfoOutlinedIcon sx={{ fontSize: 18 }} />}
        sx={{
            background: "#f7f8fa",
            color: "#586178",
            fontWeight: 500,
            boxShadow: "none",
            border: "none",
            py: 0.6,
            mb: 0,
            fontSize: 13,
            alignItems: 'center',
            width: 430
        }}
    >
        AI insights provided for information only.
    </Alert>
);

const ExpenseEditPage: React.FC<ExpenseEditProps> = ({
    // initialValues,
    // onSave,
}) => {

    // const [payeeError, setPayeeError] = useState<string | null>(null);
    const [categoryTip, setCategoryTip] = useState<string | null>(null);
    const [totalTip, setTotalTip] = useState<string | null>(null);
    const [totalAnomaly, setTotalAnomaly] = useState<React.ReactNode | null>(null);
    const [showAiDisclaimer, setShowAiDisclaimer] = useState(false);
    const [anchorEl, setAnchorEl] = useState<null | HTMLElement>(null);
    useEffect(() => {
        if (anchorEl) {
            const timer = setTimeout(() => setAnchorEl(null), 2500); // 2.5秒后关闭
            return () => clearTimeout(timer);
        }
    }, [anchorEl]);
    const [payeePopoverAnchor, setPayeePopoverAnchor] = useState<null | HTMLElement>(null);
    useEffect(() => {
    if (payeePopoverAnchor) {
        const timer = setTimeout(() => setPayeePopoverAnchor(null), 2500);
        return () => clearTimeout(timer);
    }
}, [payeePopoverAnchor]);

    const [snackbar, setSnackbar] = useState<{ open: boolean; message: string }>({
        open: false,
        message: "",
    });

    const { id } = useParams<{ id?: string }>(); // 获取URL中的id参数
    const isEdit = !!id; // 是否编辑模式

    const [formData, setFormData] = useState<ExpenseFormData>({
        date: null,
        type: "Expense",
        payee: "",
        category: "",
        total: "",
        description: "",
        files: [],
    });

    // 画面初期，若id存在自动查询并填充
    useEffect(() => {
        if (isEdit && id) {
            fetch(`/api/Expense/${id}`)
                .then(res => res.json())
                .then((data: ExpenseFormData) => {
                    // 需要把date字段转成dayjs对象
                    setFormData({
                        ...data,
                        date: data.date ? dayjs(data.date) : null,
                        files: [],  // 查询时一般不用带files
                    });
                });
        }
    }, [isEdit, id]);

    const handleChange =
        (field: keyof ExpenseFormData) =>
            (event: React.ChangeEvent<HTMLInputElement | { name?: string; value: unknown }>) => {
                setFormData((prev) => ({
                    ...prev,
                    [field]: event.target.value,
                }));
            };

    const handleDateChange = (date: Dayjs | null) => {
        setFormData((prev) => ({
            ...prev,
            date,
        }));
    };

    const handleFilesChange = (event: React.ChangeEvent<HTMLInputElement>) => {
        const filesArray = event.target.files ? Array.from(event.target.files) : [];
        setFormData((prev) => ({
            ...prev,
            files: [...(prev.files || []), ...filesArray],
        }));
    };

    const handleSave = async () => {
        // 前置必填校验
        if (!formData.date) {
            setSnackbar({ open: true, message: "Please select a Date before saving." });
            return;
        }
        if (!formData.type || formData.type.trim() === "") {
            setSnackbar({ open: true, message: "Please select an Expense Type before saving." });
            return;
        }
        if (!formData.payee || formData.payee.trim() === "") {
            setSnackbar({ open: true, message: "Please specify the Payee before saving." });
            return;
        }
        if (!formData.total || formData.total.toString().trim() === "") {
            setSnackbar({ open: true, message: "Please enter the Total amount before saving." });
            return;
        }

        // 构建发送用的数据体，不需要files字段
        const payload = {
            ...formData,
            date: formData.date ? formData.date.format("YYYY-MM-DD") : null,
            files: undefined, // 不发送文件
        };

        if (isEdit && id) {
            // 修改模式: 调用PUT
            await fetch(`/api/Expense/${id}`, {
                method: "PUT",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ ...payload, id: Number(id) }),
            });
        } else {
            // 新建模式: 调用POST
            await fetch(`/api/Expense`, {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(payload),
            });
        }
        // 可以加页面跳转、关闭弹窗等回调
        // if (onSave) onSave(formData); // 如需回调
    };

    const handleCancel = () => {
        setFormData({
            date: null,
            type: "Expense",
            payee: "",
            category: "",
            total: "",
            description: "",
            files: [],
        });
        setCategoryIsAutoFilled(false);
        setCategoryScore(null);
        // setPayeeError(null);
        setTotalTip(null);
        setCategoryTip(null);
        setTotalAnomaly(null);
        setShowAiDisclaimer(false);
    };

    const handleSelectChange =
        (field: keyof ExpenseFormData) =>
            (event: SelectChangeEvent<string>) => {
                setFormData((prev) => ({
                    ...prev,
                    [field]: event.target.value,
                }));
                if (field === "category") setCategoryIsAutoFilled(false);
            };

    const [categoryIsAutoFilled, setCategoryIsAutoFilled] = useState(false);
    const [categoryScore, setCategoryScore] = useState<number | null>(null);

    const handleAutoCategory = async (e?: React.MouseEvent<HTMLElement>) => {
        // 先清空提示
        // setPayeeError(null);
        // setTotalTip(null);
        setCategoryTip(null);

        // payee必填
        if (!formData.payee || formData.payee.trim() === "") {
            // setPayeeError("Please specify a Payee before using auto-categorization.");
            if (e) setPayeePopoverAnchor(e.currentTarget);
            return;
        }

        // total 为空仅提示，不阻断
        if (!formData.total) {
            setCategoryTip("No Total amount entered. Results may be less accurate.");
            // 继续向后端请求
        }

        // 构建 payload，并去掉 total: "" 或 null
        const payload: any = { ...formData };
        if (payload.total === "" || payload.total == null) {
            delete payload.total;
        }

        // 处理 date
        if (!payload.date) {
            delete payload.date;
        }

        // 这里假设 getAutoCategory 是你的AI接口，可以自行修改
        const aiResult = await getAutoCategory(payload);
        setFormData(prev => ({ ...prev, category: aiResult.category }));
        setCategoryIsAutoFilled(true);
        setCategoryScore(aiResult.score);
    };

    // 自动分类API 调用
    async function getAutoCategory(expense: ExpenseFormData): Promise<{ category: string; score: number }> {
        // console.log('Payload for auto-category:', expense);

        const resp = await fetch('/api/Expense/auto-category', { // 请根据实际后端接口修改URL!!!!
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(expense)
        });
        const data = await resp.json();
        console.log('Auto-category API response:', data);
        return data.category;
    }

    type RiskLevel = '高风险' | '中风险' | '低风险' | '正常范围' | '分数不可用';

    const riskColorMap: Record<RiskLevel, string> = {
        '高风险': '#d32f2f',
        '中风险': '#ffa726',
        '低风险': '#29b6f6',
        '正常范围': '#2e7d32',
        '分数不可用': '#757575'
    };

    function getRiskPrefix(riskLevel: RiskLevel) {
        switch (riskLevel) {
            case '高风险': return '⚠️';
            case '中风险': return '❗';
            case '低风险': return '💡';
            case '正常范围': return '🛡️';
            default: return '';
        }
    }

    // === 核心：异常解释提示 ===
    function getAnomalyHint(result: any): string {
        if (result.riskLevel === '高风险') {
            return result.method === "ZScore"
                ? "该金额偏离历史平均水平，疑似异常金额。"
                : "本单的金额、收款方、类型等综合特征，与历史账单综合表现出较大差异，疑似综合异常。";
        }
        if (result.riskLevel === '中风险') {
            return result.method === "ZScore"
                ? "该金额较为偏离历史平均水平，请关注。"
                : "本单部分特征与历史账单有所不同，请关注。";
        }
        if (result.riskLevel === '低风险') {
            return "数据略有波动，属于可接受范围。";
        }
        // 正常范围或分数不可用
        return "";
    }

    async function checkTotalAnomaly(total: string, payee: string, type: string) {
        if (!payee?.trim() || !type?.trim()) {
            setTotalTip("Complete Type and Payee for more accurate AI anomaly detection.");
            setTotalAnomaly(null);
            setShowAiDisclaimer(false);
            return;
        }

        if (!total || isNaN(Number(total))) {
            setTotalAnomaly(null);
            setShowAiDisclaimer(false);
            return;
        }

        // // 如果没填写金额，或不是有效数字，直接清除异常提示（onblur only）
        // if (!requireTotalCheck && (!total || isNaN(Number(total)))) {
        //     setTotalAnomaly(null);
        //     setShowAiDisclaimer(false);
        //     return;
        // }
        // // 如果是点击按钮强制检测，则必须金额有效
        // if (requireTotalCheck && (!total || isNaN(Number(total)))) {
        //     setSnackbar({ open: true, message: 'Please enter a valid Total amount before anomaly detection.' });
        //     setTotalAnomaly(null);
        //     setShowAiDisclaimer(false);
        //     return;
        // }

        const payload = {
            total: Number(total),
            payee: payee,
            type: type
        };
        const resp = await fetch('/api/Expense/detect-single', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });
        const data = await resp.json();
        const result = data?.result;
        // console.log('Anomaly API result:', result);

        // 友好提示
        if (!result) {
            setTotalAnomaly('未获取到检测结果');
            return;
        }

        setShowAiDisclaimer(true);
        setTotalTip(null);
        const riskLevel = (result.riskLevel as RiskLevel) || '';
        const prefix = riskLevel ? getRiskPrefix(riskLevel as RiskLevel) : '';
        const color = riskLevel ? riskColorMap[riskLevel as RiskLevel] : '#757575';
        const scoreStr = (typeof result.score === 'number' && !isNaN(result.score))
            ? result.score.toFixed(2)
            : ''; // 只有真的不可用才空串
        const methodDesc = result.method === "ZScore" ? "Z-Score统计" : "智能检测";
        const hint = getAnomalyHint(result);

        // 只在 riskLevel === '分数不可用' 时走兜底（"未能判定"），其他情况都应正常显示颜色分级
        // if (!riskLevel || riskLevel === '分数不可用') {
        //     setTotalAnomaly(
        //         <span style={{ color: '#7d8594', fontSize: 14 }}>
        //             No risk level or score could be determined for this record.<br />
        //             This may be because there is insufficient historical data, or because this expense <br />does not appear anomalous based on current data.
        //         </span>
        //     );
        //     return;
        // }
        if (!riskLevel || riskLevel === '分数不可用') {
            setTotalAnomaly(
                <Alert
                    severity="info"
                    icon={<InfoOutlinedIcon sx={{ fontSize: 18 }} />}
                    sx={{
                        background: "#f7f8fa",
                        color: "#7d8594",
                        fontWeight: 500,
                        boxShadow: "none",
                        border: "none",
                        py: 0.6,
                        mb: 0,
                        fontSize: 13,
                        alignItems: 'center',
                        width: 430
                    }}
                >
                    No risk level or score could be determined for this record.
                    <br />
                    This may be because there is insufficient historical data, or because this expense
                    does not appear anomalous based on current data.
                </Alert>
            );
            return;
        }

        // 正常分级都用颜色和emoji显示
        // setTotalAnomaly(
        //     <span style={{ color }}>
        //         {prefix} {riskLevel}，分数: {scoreStr}（{methodDesc}）{hint}
        //     </span>
        // );
        setTotalAnomaly(
            <Alert
                severity={"warning"} // or "info" as needed
                icon={prefix ? <span style={{ fontSize: 20 }}>{prefix}</span> : <InfoOutlinedIcon sx={{ fontSize: 18 }} />}
                sx={{
                    background: "#f7f8fa",
                    color,
                    fontWeight: 500,
                    boxShadow: "none",
                    border: "none",
                    py: 0.6,
                    mb: 0,
                    fontSize: 13,
                    alignItems: 'center',
                    width: 430
                }}
            >
                {riskLevel}，分数: {scoreStr}（{methodDesc}）{hint}
            </Alert>
        );
    }

    return (
        <Paper sx={{ maxWidth: 600, margin: "40px auto", p: 3 }}>
            <Typography variant="h5" sx={{ mb: 3 }}>
                Edit Expense
            </Typography>
            <Stack spacing={2}>
                {/* Date */}
                <DatePicker
                    label="Date"
                    value={formData.date}
                    onChange={handleDateChange}
                    sx={{ width: 460 }}
                />
                {/* Type */}
                <FormControl sx={{ width: 460 }}>
                    <InputLabel id="type-label">Type</InputLabel>
                    <Select
                        labelId="type-label"
                        value={formData.type}
                        label="Type"
                        onChange={handleSelectChange("type")}
                    >
                        {typeOptions.map((t) => (
                            <MenuItem key={t} value={t}>{t}</MenuItem>
                        ))}
                    </Select>
                </FormControl>
                {/* Payee */}
                <FormControl sx={{ width: 460 }}>
                    <InputLabel id="payee-label">Payee</InputLabel>
                    <Select
                        labelId="payee-label"
                        value={formData.payee}
                        label="Payee"
                        onChange={handleSelectChange("payee")}
                        renderValue={(value) => value as string}
                        MenuProps={{ PaperProps: { sx: { maxHeight: 200 } } }}
                    >
                        {payeeOptions.map((payee) => (
                            <MenuItem key={payee} value={payee}>
                                {payee}
                            </MenuItem>
                        ))}
                    </Select>
                </FormControl>
                {/* Category */}
                <Box display="flex" alignItems="center">
                    <FormControl sx={{ width: 460 }}>
                        <InputLabel id="category-label">Category</InputLabel>
                        <Select
                            labelId="category-label"
                            value={formData.category}
                            label="Category"
                            onChange={handleSelectChange("category")}
                        >
                            {categoryOptions.map((cat) => (
                                <MenuItem key={cat} value={cat}>{cat}</MenuItem>
                            ))}
                        </Select>
                        {categoryTip && (
                            <Typography variant="caption" color="warning.main" sx={{ mt: 0.5 }}>
                                {categoryTip}
                            </Typography>
                        )}
                    </FormControl>
                    {/* 与输入框有一致的间隔，比如8或12像素 */}
                    <Tooltip title="AI自动分类">
                        <IconButton
                            size="small"
                            onClick={e => handleAutoCategory(e)}
                            sx={{
                                ml: 1.5,
                                mt: "4px",
                                background: "none",
                                boxShadow: "none",
                                border: "none",
                                outline: "none",
                                '&:hover': { background: 'none' }
                                // '&:focus': { outline: 'none' }
                            }}
                            disableRipple
                        >
                            <AutoAwesomeIcon color="primary" />
                        </IconButton>
                    </Tooltip>
                    <Popover
    open={Boolean(payeePopoverAnchor)}
    anchorEl={payeePopoverAnchor}
    onClose={() => setPayeePopoverAnchor(null)}
    anchorOrigin={{ vertical: "bottom", horizontal: "center" }}
    transformOrigin={{ vertical: "top", horizontal: "center" }}
    PaperProps={{
        sx: {
            background: "#faf6ef",
            color: "#9a7b3d",
            fontWeight: 500,
            boxShadow: "none",
            border: "none",
            fontSize: 13,
            px: 2,
            py: 0.7,
        }
    }}
>
    Please specify a Payee before using auto-categorization.
</Popover>
                </Box>
                {/* 这里做payee error和total tip的下方提示 */}
                {/* {payeeError && (
                    <Typography variant="caption" color="error">{payeeError}</Typography>
                )} */}
                {categoryIsAutoFilled && (
                    <Alert
                        severity="info"
                        icon={<AutoAwesomeIcon color="primary" fontSize="small" sx={{ mt: "2px" }} />}
                        sx={{
                            background: "#f7f8fa",
                            color: "#586178",
                            fontWeight: 500,
                            boxShadow: "none",
                            border: "none",
                            py: 0.6,
                            mb: 0,
                            fontSize: 13,
                            alignItems: 'center',
                            width: 430, // 或和你的表单宽度统一
                            mt: 0.5
                        }}
                    >
                        <Tooltip title="已根据内容自动推荐类别，可手动更改">
                            <span>
                                已自动推荐Category
                                {categoryScore !== null && (
                                    <Typography
                                        component="span"
                                        sx={{
                                            ml: 1,
                                            fontSize: 13,
                                            color:
                                                categoryScore < 0.4
                                                    ? 'error.main'
                                                    : categoryScore < 0.7
                                                        ? 'warning.main'
                                                        : 'info.main'
                                        }}
                                    >
                                        (置信度: {(categoryScore * 100).toFixed(1)}%)
                                    </Typography>
                                )}
                            </span>
                        </Tooltip>
                    </Alert>
                )}
                {/* Total */}
                <Box>
                    <TextField
                        label="Total ($)"
                        type="text"
                        value={formData.total ?? ""}
                        onChange={e => {
                            const value = e.target.value;
                            // 只允许正整数、空字符串
                            if (/^\d*$/.test(value)) {
                                setFormData(prev => ({ ...prev, total: value }));
                            }
                        }}
                        inputProps={{
                            inputMode: "numeric",
                            pattern: "[0-9]*"
                        }}
                        sx={{ width: 460 }}
                        onBlur={e => {
                            checkTotalAnomaly(
                                e.target.value,
                                formData.payee,
                                formData.type
                            );
                            // if (!showAiDisclaimer && e.target.value && !isNaN(Number(e.target.value))) {
                            //     setShowAiDisclaimer(true);
                            // }
                        }}

                    />
                    <Tooltip title="AI异常检测">
                        <IconButton
                        size="small"
                            onClick={e => {
                                if (!formData.total || isNaN(Number(formData.total))) {
                                    setAnchorEl(e.currentTarget); // 绑定到当前按钮
                                    return;
                                }
                                checkTotalAnomaly(formData.total ?? '', formData.payee, formData.type);
                            }}
                            sx={{
                                ml: 1.5,
                                mt: "4px",
                                background: "none",
                                boxShadow: "none",
                                border: "none",
                                outline: "none",
                                '&:hover': { background: 'none' }
                                // '&:focus': { outline: 'none' }
                            }}
                            disableRipple
                        >
                            <WarningAmberOutlinedIcon color="warning" fontSize="small" />
                        </IconButton>
                    </Tooltip>
                    <Popover
                        open={Boolean(anchorEl)}
                        anchorEl={anchorEl}
                        onClose={() => setAnchorEl(null)}
                        anchorOrigin={{ vertical: "bottom", horizontal: "center" }}
                        transformOrigin={{ vertical: "top", horizontal: "center" }}
                        PaperProps={{
                            sx: {
                                background: "#faf6ef",
                                color: "#9a7b3d",
                                fontWeight: 500,
                                boxShadow: "none",
                                border: "none",
                                fontSize: 13,
                                px: 2,
                                py: 0.7,
                            }
                        }}
                    >
                        Please enter a valid Total amount before anomaly detection.
                    </Popover>
                </Box>


                {totalTip && (
                    <Typography
                        variant="caption"
                        color="warning.main" // 这里使用橙色
                        sx={{ mt: 0.5 }}
                    >
                        {totalTip}
                    </Typography>
                )}
                {showAiDisclaimer && aiDisclaimer}
                {totalAnomaly && (
                    <div style={{ marginTop: 6 }}>
                        {totalAnomaly}
                    </div>
                )}
                {/* Description */}
                <TextField
                    label="Description"
                    multiline
                    rows={2}
                    value={formData.description}
                    onChange={handleChange("description")}
                    sx={{ width: 460 }}
                />
                {/* File upload */}
                <Box>
                    <Button
                        variant="outlined"
                        component="label"
                        startIcon={<UploadFileIcon />}
                    >
                        Upload File(s)
                        <input
                            type="file"
                            hidden
                            multiple
                            accept=".pdf,.jpg,.jpeg,.png"
                            onChange={handleFilesChange}
                        />
                    </Button>
                    <Box sx={{ mt: 1 }}>
                        {formData.files &&
                            formData.files.map((file, idx) => (
                                <Typography key={idx} variant="caption">
                                    {file.name}
                                </Typography>
                            ))}
                    </Box>
                </Box>
                {/* Action Buttons */}
                <Stack direction="row" spacing={2} justifyContent="flex-end" sx={{ pt: 2 }}>
                    <Button variant="outlined" color="inherit" onClick={handleCancel}>
                        Cancel
                    </Button>
                    <Button variant="contained" color="primary" onClick={handleSave}>
                        Save
                    </Button>
                </Stack>
            </Stack>
            <Snackbar
                open={snackbar.open}
                message={snackbar.message}
                autoHideDuration={3000}
                onClose={() => setSnackbar(s => ({ ...s, open: false }))}
                anchorOrigin={{ vertical: "bottom", horizontal: "center" }}
            />
        </Paper>
    );
};

export default ExpenseEditPage;