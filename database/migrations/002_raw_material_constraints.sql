-- =========================================================
-- SIKOPY: Raw material validation constraints
-- =========================================================

BEGIN;

ALTER TABLE biji_kopi_mentah
    ADD CONSTRAINT ck_biji_kopi_mentah_stok_non_negative
    CHECK (stok_kg >= 0);

ALTER TABLE biji_kopi_mentah
    ADD CONSTRAINT ck_biji_kopi_mentah_harga_non_negative
    CHECK (harga_per_kg >= 0);

ALTER TABLE riwayat_stok
    ADD CONSTRAINT ck_riwayat_stok_one_product
    CHECK ((id_biji_mentah IS NOT NULL) <> (id_roasted IS NOT NULL));

ALTER TABLE riwayat_stok
    ADD CONSTRAINT ck_riwayat_stok_jumlah_positive
    CHECK (jumlah > 0);

COMMIT;
