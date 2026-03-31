import React, { useEffect, useMemo, useState } from "react";
import type { SelectChangeEvent } from "@mui/material/Select";
import dayjs, { Dayjs } from "dayjs";
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  FormControl,
  IconButton,
  InputLabel,
  MenuItem,
  Paper,
  Popover,
  Select,
  Snackbar,
  Stack,
  TextField,
  Tooltip,
  Typography,
} from "@mui/material";
import { DatePicker } from "@mui/x-date-pickers/DatePicker";
import UploadFileIcon from "@mui/icons-material/UploadFile";
import AutoAwesomeIcon from "@mui/icons-material/AutoAwesome";
import WarningAmberRoundedIcon from "@mui/icons-material/WarningAmberRounded";
import LightbulbRoundedIcon from "@mui/icons-material/LightbulbRounded";
import { useParams } from "react-router-dom";

type ExpenseType = "Expense" | "Bill" | "Check" | "PurchaseOrder";
type RiskLevel = "高风险" | "中风险" | "低风险" | "正常范围" | "分数不可用";

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

type CategorySuggestion = {
  category: string;
  score: number;
};

type AnomalyResultDto = {
  isAnomaly?: boolean;
  score?: number;
  method?: string;
  riskLevel?: RiskLevel | string;
  historicalAverage?: number;
  currentAmount?: number;
  deviationPercent?: number;
  reason?: string;
  suggestions?: string[];
};

type AiInsightDto = {
  summary: string;
  causes: string[];
  actions: string[];
  model?: string;
  fallbackReason?: string;
};

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
  "--Split--",
];

const typeOptions: ExpenseType[] = ["Expense", "Bill", "Check", "PurchaseOrder"];

function formatUsd(value?: number | null): string {
  if (value === null || value === undefined || Number.isNaN(value)) return "--";
  return "$" + value.toLocaleString(undefined, { maximumFractionDigits: 2 });
}

function getRiskTone(level?: string): "error" | "warning" | "info" | "success" {
  if (level === "高风险") return "error";
  if (level === "中风险") return "warning";
  if (level === "低风险" || level === "分数不可用") return "info";
  return "success";
}

function riskHeadline(level?: string): string {
  if (level === "高风险") return "High Risk Detected";
  if (level === "中风险") return "Medium Risk Detected";
  if (level === "低风险") return "Low Risk Detected";
  return "No Strong Risk Signal";
}

function riskDescription(level?: string): string {
  if (level === "高风险") return "This amount is significantly higher than your typical expenses.";
  if (level === "中风险") return "This amount is noticeably above your normal expense range.";
  if (level === "低风险") return "This amount is slightly above expected range. Monitor this transaction.";
  return "No abnormal pattern was strongly detected for this amount.";
}

const ExpenseEditPage: React.FC<ExpenseEditProps> = () => {
  const { id } = useParams<{ id?: string }>();
  const isEdit = !!id;

  const [formData, setFormData] = useState<ExpenseFormData>({
    date: null,
    type: "Expense",
    payee: "",
    category: "",
    total: "",
    description: "",
    files: [],
  });

  const [categoryTip, setCategoryTip] = useState<string | null>(null);
  const [totalTip, setTotalTip] = useState<string | null>(null);
  const [snackbar, setSnackbar] = useState<{ open: boolean; message: string }>({ open: false, message: "" });
  const [payeePopoverAnchor, setPayeePopoverAnchor] = useState<null | HTMLElement>(null);
  const [amountPopoverAnchor, setAmountPopoverAnchor] = useState<null | HTMLElement>(null);

  const [categorySuggestion, setCategorySuggestion] = useState<CategorySuggestion | null>(null);
  const [categoryAccepted, setCategoryAccepted] = useState(false);

  const [anomaly, setAnomaly] = useState<AnomalyResultDto | null>(null);
  const [aiInsight, setAiInsight] = useState<AiInsightDto | null>(null);
  const [aiInsightLoading, setAiInsightLoading] = useState(false);
  const [aiInsightError, setAiInsightError] = useState<string | null>(null);

  useEffect(() => {
    if (isEdit && id) {
      fetch(`/api/Expense/${id}`)
        .then((res) => res.json())
        .then((data: ExpenseFormData) => {
          setFormData({
            ...data,
            date: data.date ? dayjs(data.date) : null,
            files: [],
            total: data.total ? String(data.total) : "",
          });
        });
    }
  }, [id, isEdit]);

  useEffect(() => {
    if (payeePopoverAnchor) {
      const timer = setTimeout(() => setPayeePopoverAnchor(null), 2200);
      return () => clearTimeout(timer);
    }
  }, [payeePopoverAnchor]);

  useEffect(() => {
    if (amountPopoverAnchor) {
      const timer = setTimeout(() => setAmountPopoverAnchor(null), 2200);
      return () => clearTimeout(timer);
    }
  }, [amountPopoverAnchor]);

  const confidencePct = useMemo(() => {
    if (!categorySuggestion) return null;
    return Math.round(categorySuggestion.score * 1000) / 10;
  }, [categorySuggestion]);

  const handleChange =
    (field: keyof ExpenseFormData) =>
    (event: React.ChangeEvent<HTMLInputElement | { value: unknown }>) => {
      setFormData((prev) => ({ ...prev, [field]: event.target.value }));
    };

  const handleSelectChange =
    (field: keyof ExpenseFormData) =>
    (event: SelectChangeEvent<string>) => {
      setFormData((prev) => ({ ...prev, [field]: event.target.value }));
      if (field === "category") {
        setCategoryAccepted(false);
      }
    };

  const handleDateChange = (date: Dayjs | null) => {
    setFormData((prev) => ({ ...prev, date }));
  };

  const handleFilesChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    const filesArray = event.target.files ? Array.from(event.target.files) : [];
    setFormData((prev) => ({ ...prev, files: [...(prev.files || []), ...filesArray] }));
  };

  async function getAutoCategory(expense: ExpenseFormData): Promise<CategorySuggestion> {
    const resp = await fetch("/api/Expense/auto-category", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(expense),
    });
    const data = await resp.json();
    const payload = data?.category ?? data;

    return {
      category: payload?.category ?? "",
      score: Number(payload?.score ?? 0),
    };
  }

  const handleAutoCategory = async (e?: React.MouseEvent<HTMLElement>) => {
    setCategoryTip(null);

    if (!formData.payee || formData.payee.trim() === "") {
      if (e) setPayeePopoverAnchor(e.currentTarget);
      return;
    }

    const payload: any = { ...formData };
    if (payload.total === "" || payload.total == null) delete payload.total;
    if (!payload.date) delete payload.date;

    const aiResult = await getAutoCategory(payload);
    setCategorySuggestion(aiResult);
    setCategoryAccepted(false);

    if (aiResult.category) {
      setFormData((prev) => ({ ...prev, category: aiResult.category }));
    }
  };

  const acceptCategorySuggestion = () => {
    if (!categorySuggestion) return;
    setFormData((prev) => ({ ...prev, category: categorySuggestion.category }));
    setCategoryAccepted(true);
  };

  async function fetchAiInsight(category: string, current: number, avg: number, deviation: number, riskLevel: string) {
    setAiInsightLoading(true);
    setAiInsightError(null);
    try {
      const resp = await fetch("/api/Ai/explain-anomaly", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          category,
          current,
          avg,
          deviation,
          riskLevel,
        }),
      });

      if (resp.ok === false) {
        throw new Error("AI explanation request failed.");
      }

      const data = (await resp.json()) as AiInsightDto;
      if (!data?.summary) {
        throw new Error("AI explanation response is empty.");
      }

      setAiInsight({
        summary: data.summary,
        causes: Array.isArray(data.causes) ? data.causes : [],
        actions: Array.isArray(data.actions) ? data.actions : [],
        model: data.model,
        fallbackReason: data.fallbackReason,
      });
    } catch (error) {
      setAiInsight(null);
      setAiInsightError(error instanceof Error ? error.message : "AI insight is currently unavailable.");
    } finally {
      setAiInsightLoading(false);
    }
  }

  async function checkTotalAnomaly(total: string, payee: string, type: string) {
    if (!payee?.trim() || !type?.trim()) {
      setTotalTip("Complete Type and Payee for more accurate AI anomaly detection.");
      setAnomaly(null);
      setAiInsight(null);
      setAiInsightError(null);
      return;
    }

    if (!total || isNaN(Number(total))) {
      setAnomaly(null);
      setAiInsight(null);
      setAiInsightError(null);
      return;
    }

    const payload = {
      total: Number(total),
      payee,
      type,
      category: formData.category,
      description: formData.description,
      date: formData.date ? formData.date.format("YYYY-MM-DD") : undefined,
    };

    const resp = await fetch("/api/Expense/detect-single", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload),
    });

    const data = await resp.json();
    const result: AnomalyResultDto | undefined = data?.result;

    if (!result) {
      setAnomaly(null);
      setAiInsight(null);
      setAiInsightError(null);
      return;
    }

    const current = Number(total);
    const avg = result.historicalAverage ?? 0;
    const deviation = result.deviationPercent ?? (avg === 0 ? 0 : ((current - avg) / avg) * 100);

    const normalized: AnomalyResultDto = {
      ...result,
      currentAmount: result.currentAmount ?? current,
      historicalAverage: avg,
      deviationPercent: deviation,
      suggestions:
        result.suggestions && result.suggestions.length > 0
          ? result.suggestions
          : ["Verify the entered amount.", "Check invoice details.", "Confirm category assignment."],
    };

    setTotalTip(null);
    setAnomaly(normalized);

    const risk = (normalized.riskLevel ?? "").toString();
    const riskIndicatesAlert = risk !== "" && risk !== "正常范围";
    const deviationIndicatesAlert = Math.abs(normalized.deviationPercent ?? 0) >= 30;
    const shouldExplainWithAi = riskIndicatesAlert || deviationIndicatesAlert;

    if (shouldExplainWithAi) {
      await fetchAiInsight(
        formData.category || "Uncategorized",
        normalized.currentAmount ?? current,
        normalized.historicalAverage ?? avg,
        normalized.deviationPercent ?? deviation,
        normalized.riskLevel ?? "Unknown");
    } else {
      setAiInsight(null);
      setAiInsightError(null);
    }
  }

  const handleSave = async () => {
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

    const payload = {
      ...formData,
      date: formData.date ? formData.date.format("YYYY-MM-DD") : null,
      files: undefined,
      total: formData.total ? Number(formData.total) : null,
    };

    if (isEdit && id) {
      await fetch(`/api/Expense/${id}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ ...payload, id: Number(id) }),
      });
    } else {
      await fetch(`/api/Expense`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
      });
    }

    setSnackbar({ open: true, message: "Expense saved." });
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
    setCategoryTip(null);
    setTotalTip(null);
    setCategorySuggestion(null);
    setCategoryAccepted(false);
    setAnomaly(null);
    setAiInsight(null);
    setAiInsightError(null);
  };

  return (
    <Paper
      sx={{
        width: "100%",
        maxWidth: 960,
        minWidth: { xs: "100%", md: 760 },
        margin: "24px auto",
        p: { xs: 2, md: 3.5 },
        borderRadius: 2.5,
        border: "1px solid #dfe5f2",
      }}
    >
      <Typography variant="h4" sx={{ mb: 3, fontWeight: 700 }}>
        Edit Expense
      </Typography>

      <Stack spacing={2}>
        <DatePicker label="Date" value={formData.date} onChange={handleDateChange} sx={{ width: "100%" }} />

        <FormControl fullWidth>
          <InputLabel id="type-label">Type</InputLabel>
          <Select labelId="type-label" value={formData.type} label="Type" onChange={handleSelectChange("type")}>
            {typeOptions.map((t) => (
              <MenuItem key={t} value={t}>
                {t}
              </MenuItem>
            ))}
          </Select>
        </FormControl>

        <FormControl fullWidth>
          <InputLabel id="payee-label">Payee</InputLabel>
          <Select labelId="payee-label" value={formData.payee} label="Payee" onChange={handleSelectChange("payee")}>
            {payeeOptions.map((payee) => (
              <MenuItem key={payee} value={payee}>
                {payee}
              </MenuItem>
            ))}
          </Select>
        </FormControl>

        <Card variant="outlined" sx={{ borderRadius: 2 }}>
          <CardContent sx={{ pb: "16px !important" }}>
            <Stack direction="row" alignItems="center" justifyContent="space-between" sx={{ mb: 1.2 }}>
              <Typography variant="h6" fontWeight={700}>
                Category
              </Typography>
              <Tooltip title="AI auto category">
                <IconButton onClick={(e) => void handleAutoCategory(e)} size="small">
                  <AutoAwesomeIcon color="primary" />
                </IconButton>
              </Tooltip>
            </Stack>

            <FormControl fullWidth>
              <InputLabel id="category-label">Category</InputLabel>
              <Select labelId="category-label" value={formData.category} label="Category" onChange={handleSelectChange("category")}>
                {categoryOptions.map((cat) => (
                  <MenuItem key={cat} value={cat}>
                    {cat}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>

            {categorySuggestion ? (
              <Box sx={{ mt: 1.5, p: 1.5, borderRadius: 1.8, bgcolor: "#f5f8ff", border: "1px solid #d8e3ff" }}>
                <Stack direction={{ xs: "column", sm: "row" }} spacing={1} alignItems={{ xs: "flex-start", sm: "center" }} justifyContent="space-between">
                  <Stack direction="row" spacing={1} alignItems="center" flexWrap="wrap">
                    <Chip label={categorySuggestion.category || "Recommended"} color="primary" variant="outlined" />
                    <Chip label={`${confidencePct ?? 0}%`} size="small" />
                    <Typography variant="body2" color="text.secondary">
                      Recommended
                    </Typography>
                  </Stack>

                  <Stack direction="row" spacing={1}>
                    <Button variant="outlined" size="small" onClick={acceptCategorySuggestion}>
                      Accept
                    </Button>
                    <Button variant="contained" size="small" onClick={() => setCategoryAccepted(false)}>
                      Change
                    </Button>
                  </Stack>
                </Stack>

                <Typography variant="body2" sx={{ mt: 1, color: "#4f5f80" }}>
                  <AutoAwesomeIcon sx={{ fontSize: 16, mr: 0.6, verticalAlign: "middle" }} />
                  Based on similar transactions and historical patterns.
                  {categoryAccepted ? " Accepted." : ""}
                </Typography>
              </Box>
            ) : null}

            {categoryTip ? (
              <Typography variant="caption" color="warning.main" sx={{ mt: 1, display: "block" }}>
                {categoryTip}
              </Typography>
            ) : null}
          </CardContent>
        </Card>

        <Box>
          <TextField
            label="Total ($)"
            type="text"
            value={formData.total ?? ""}
            onChange={(e) => {
              const value = e.target.value;
              if (/^\d*$/.test(value)) {
                setFormData((prev) => ({ ...prev, total: value }));
              }
            }}
            onBlur={(e) => {
              void checkTotalAnomaly(e.target.value, formData.payee, formData.type);
            }}
            fullWidth
            inputProps={{ inputMode: "numeric", pattern: "[0-9]*" }}
          />

          <Stack direction="row" justifyContent="flex-end" sx={{ mt: 0.6 }}>
            <Tooltip title="AI anomaly check">
              <IconButton
                size="small"
                onClick={(e) => {
                  if (!formData.total || isNaN(Number(formData.total))) {
                    setAmountPopoverAnchor(e.currentTarget);
                    return;
                  }
                  void checkTotalAnomaly(formData.total ?? "", formData.payee, formData.type);
                }}
              >
                <WarningAmberRoundedIcon color="warning" fontSize="small" />
              </IconButton>
            </Tooltip>
          </Stack>
        </Box>

        {totalTip ? (
          <Typography variant="caption" color="warning.main">
            {totalTip}
          </Typography>
        ) : null}

        {anomaly ? (
          <Card variant="outlined" sx={{ borderRadius: 2, borderColor: "#e3d8bf", bgcolor: "#fffdf8" }}>
            <CardContent>
              <Stack direction="row" alignItems="center" justifyContent="space-between" sx={{ mb: 1 }}>
                <Stack direction="row" spacing={1} alignItems="center">
                  <WarningAmberRoundedIcon color="warning" />
                  <Typography variant="h6" sx={{ fontWeight: 700 }}>
                    {formData.total || "0"}
                  </Typography>
                </Stack>
                <Chip label={riskHeadline(anomaly.riskLevel)} color={getRiskTone(anomaly.riskLevel)} />
              </Stack>

              <Typography variant="h5" sx={{ mb: 0.6, fontWeight: 700 }}>
                {riskHeadline(anomaly.riskLevel)}
              </Typography>
              <Typography variant="body1" sx={{ color: "#4d5668", mb: 1.2 }}>
                {anomaly.reason || riskDescription(anomaly.riskLevel)}
              </Typography>

              <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 1.2 }}>
                <Box>
                  <Typography>• Average: {formatUsd(anomaly.historicalAverage)}</Typography>
                </Box>
                <Box>
                  <Typography>• Current: {formatUsd(anomaly.currentAmount)}</Typography>
                </Box>
                <Box>
                  <Typography>
                    • {(anomaly.deviationPercent ?? 0) >= 0
                      ? `This amount is ${(anomaly.deviationPercent ?? 0).toFixed(1)}% higher than your usual expenses.`
                      : `This amount is ${Math.abs(anomaly.deviationPercent ?? 0).toFixed(1)}% lower than your usual expenses.`}
                  </Typography>
                </Box>
                {/* Hidden for now: Method (ZScore/engine) row. */}
              </Box>

              {/* Hidden for now: View Details / Ask AI buttons and score-risk detail row. */}
            </CardContent>
          </Card>
        ) : null}

        {anomaly ? (
          <Card variant="outlined" sx={{ borderRadius: 2, bgcolor: "#f7f9ff", borderColor: "#dce4f7" }}>
            <CardContent>
              <Typography variant="h5" sx={{ mb: 1.2, fontWeight: 800 }}>
                <LightbulbRoundedIcon sx={{ mr: 0.7, color: "#f5b400", verticalAlign: "middle" }} />
                AI Insight
              </Typography>

              {aiInsightLoading ? (
                <Typography sx={{ color: "#4d5668" }}>Analyzing with AI...</Typography>
              ) : aiInsight ? (
                <Box sx={{ display: "grid", gridTemplateColumns: "1fr", gap: 2 }}>
                  <Box>
                    <Typography sx={{ mb: 0.8, whiteSpace: "normal", overflowWrap: "anywhere", wordBreak: "break-word" }}>
                      ⚠️ {aiInsight.summary}
                    </Typography>

                    <Typography sx={{ fontWeight: 700, mb: 0.6 }}>Possible causes:</Typography>
                    {aiInsight.causes.map((cause, idx) => (
                      <Typography key={idx} sx={{ whiteSpace: "normal", overflowWrap: "anywhere", wordBreak: "break-word" }}>
                        • {cause}
                      </Typography>
                    ))}

                    <Typography sx={{ fontWeight: 700, mt: 1.2, mb: 0.6 }}>Suggested actions:</Typography>
                    {aiInsight.actions.map((action, idx) => (
                      <Typography key={idx} sx={{ whiteSpace: "normal", overflowWrap: "anywhere", wordBreak: "break-word" }}>
                        • {action}
                      </Typography>
                    ))}

                    {aiInsight.model ? (
                      <Typography variant="caption" sx={{ display: "block", mt: 1, color: "#6f7e99" }}>
                        Model: {aiInsight.model}
                      </Typography>
                    ) : null}

                    {aiInsight.fallbackReason ? (
                      <Typography variant="caption" sx={{ display: "block", mt: 0.5, color: "#b45309" }}>
                        Fallback: {aiInsight.fallbackReason}
                      </Typography>
                    ) : null}
                  </Box>

                  {/* Hidden for now: Quick Actions panel. */}
                </Box>
              ) : aiInsightError ? (
                <Alert severity="warning" sx={{ borderRadius: 1.5 }}>
                  {aiInsightError}
                </Alert>
              ) : (
                <Box>
                  <Typography sx={{ mb: 0.8 }}>{anomaly.reason || "No AI explanation available."}</Typography>
                  <Typography sx={{ fontWeight: 700, mt: 1.2, mb: 0.6 }}>Suggested actions:</Typography>
                  {(anomaly.suggestions ?? []).slice(0, 3).map((action, idx) => (
                    <Typography key={idx}>• {action}</Typography>
                  ))}
                </Box>
              )}
            </CardContent>
          </Card>
        ) : null}

        <TextField label="Description" multiline rows={2} value={formData.description} onChange={handleChange("description")} fullWidth />

        <Box>
          <Button variant="outlined" component="label" startIcon={<UploadFileIcon />}>
            Upload File(s)
            <input type="file" hidden multiple accept=".pdf,.jpg,.jpeg,.png" onChange={handleFilesChange} />
          </Button>

          <Box sx={{ mt: 1 }}>
            {(formData.files || []).map((file, idx) => (
              <Typography key={idx} variant="caption" display="block">
                {file.name}
              </Typography>
            ))}
          </Box>
        </Box>

        <Stack direction="row" spacing={2} justifyContent="flex-end" sx={{ pt: 1.5 }}>
          <Button variant="outlined" color="inherit" onClick={handleCancel}>
            Cancel
          </Button>
          <Button variant="contained" onClick={handleSave}>
            Save
          </Button>
        </Stack>
      </Stack>

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
          },
        }}
      >
        Please specify a Payee before using auto-categorization.
      </Popover>

      <Popover
        open={Boolean(amountPopoverAnchor)}
        anchorEl={amountPopoverAnchor}
        onClose={() => setAmountPopoverAnchor(null)}
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
          },
        }}
      >
        Please enter a valid Total amount before anomaly detection.
      </Popover>

      <Snackbar
        open={snackbar.open}
        message={snackbar.message}
        autoHideDuration={2600}
        onClose={() => setSnackbar((state) => ({ ...state, open: false }))}
        anchorOrigin={{ vertical: "bottom", horizontal: "center" }}
      />
    </Paper>
  );
};

export default ExpenseEditPage;
