function confirmDelete(message) {
    return confirm(message || "Bu işlem geri alınamaz. Devam etmek istiyor musunuz?");
}

function copyText(text) {
    navigator.clipboard.writeText(text);
    alert("Kopyalandı: " + text);
}