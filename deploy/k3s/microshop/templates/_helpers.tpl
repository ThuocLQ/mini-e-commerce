{{- define "microshop.labels" -}}
app.kubernetes.io/part-of: microshop
app.kubernetes.io/managed-by: Helm
helm.sh/chart: {{ .Chart.Name }}-{{ .Chart.Version | replace "+" "_" }}
{{- end -}}
