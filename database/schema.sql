-- =========================================================
-- SIKOPY: Sistem Inventori & Penjualan Kopi
-- Initial PostgreSQL schema
-- =========================================================

BEGIN;

-- ---------- Tabel master ----------

CREATE TABLE jenis_biji_kopi_mentah (
    id_jenis_biji_kopi_mentah SERIAL PRIMARY KEY,
    nama                      VARCHAR(100) NOT NULL
);

CREATE TABLE metode_proses (
    id_metode_proses SERIAL PRIMARY KEY,
    nama             VARCHAR(100) NOT NULL
);

CREATE TABLE pengguna (
    id_pengguna   SERIAL PRIMARY KEY,
    nama_pengguna VARCHAR(50)  NOT NULL UNIQUE,
    kata_sandi    VARCHAR(255) NOT NULL,
    nama_lengkap  VARCHAR(100) NOT NULL,
    peran         VARCHAR(30)  NOT NULL
);

-- ---------- Biji kopi mentah ----------

CREATE TABLE biji_kopi_mentah (
    id_biji_mentah            SERIAL PRIMARY KEY,
    id_jenis_biji_kopi_mentah INTEGER NOT NULL
        REFERENCES jenis_biji_kopi_mentah (id_jenis_biji_kopi_mentah),
    id_metode_proses          INTEGER NOT NULL
        REFERENCES metode_proses (id_metode_proses),
    nama_biji                 VARCHAR(100)  NOT NULL,
    asal_daerah               VARCHAR(100),
    ketinggian_mdpl           INTEGER,
    tingkat_kualitas          VARCHAR(50),
    stok_kg                   NUMERIC(10,2) NOT NULL DEFAULT 0,
    harga_per_kg              NUMERIC(12,2) NOT NULL DEFAULT 0,
    tanggal_panen             DATE,
    tanggal_masuk             TIMESTAMP     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    is_aktif                  BOOLEAN       NOT NULL DEFAULT TRUE
);

-- ---------- Batch roasting ----------

CREATE TABLE batch_roasting (
    id_batch                 SERIAL PRIMARY KEY,
    id_biji_mentah           INTEGER NOT NULL REFERENCES biji_kopi_mentah (id_biji_mentah),
    id_pengguna              INTEGER NOT NULL REFERENCES pengguna (id_pengguna),
    kode_batch               VARCHAR(50)   NOT NULL UNIQUE,
    jumlah_biji_dipakai_gram NUMERIC(10,2) NOT NULL,
    hasil_roasting_gram      NUMERIC(10,2),
    level_roasting           VARCHAR(50),
    tanggal_batch            TIMESTAMP     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    is_aktif                 BOOLEAN       NOT NULL DEFAULT TRUE,
    catatan                  TEXT
);

-- ---------- Biji kopi roasted ----------

CREATE TABLE biji_kopi_roasted (
    id_roasted     SERIAL PRIMARY KEY,
    id_batch       INTEGER NOT NULL REFERENCES batch_roasting (id_batch),
    nama_produk    VARCHAR(150)  NOT NULL,
    stok_gram      NUMERIC(10,2) NOT NULL DEFAULT 0,
    harga_per_gram NUMERIC(12,2) NOT NULL DEFAULT 0,
    status_harga   VARCHAR(30),
    is_aktif       BOOLEAN       NOT NULL DEFAULT TRUE,
    catatan        TEXT
);

-- ---------- Riwayat stok ----------
-- id_biji_mentah / id_roasted nullable: riwayat bisa untuk salah satunya

CREATE TABLE riwayat_stok (
    id_riwayat_stok SERIAL PRIMARY KEY,
    id_biji_mentah  INTEGER REFERENCES biji_kopi_mentah (id_biji_mentah),
    id_roasted      INTEGER REFERENCES biji_kopi_roasted (id_roasted),
    kategori        VARCHAR(30)   NOT NULL,
    arah            VARCHAR(20)   NOT NULL,
    jumlah          NUMERIC(10,2) NOT NULL,
    satuan          VARCHAR(10)   NOT NULL,
    referensi       VARCHAR(100),
    tanggal_riwayat TIMESTAMP     NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- ---------- Penjualan ----------

CREATE TABLE penjualan (
    id_penjualan      SERIAL PRIMARY KEY,
    id_pengguna       INTEGER NOT NULL REFERENCES pengguna (id_pengguna),
    nomor_invoice     VARCHAR(50)   NOT NULL UNIQUE,
    nama_pelanggan    VARCHAR(100),
    total_penjualan   NUMERIC(12,2) NOT NULL DEFAULT 0,
    tanggal_penjualan TIMESTAMP     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    is_aktif          BOOLEAN       NOT NULL DEFAULT TRUE
);

CREATE TABLE detail_penjualan (
    id_detail_penjualan SERIAL PRIMARY KEY,
    id_penjualan        INTEGER NOT NULL REFERENCES penjualan (id_penjualan) ON DELETE CASCADE,
    id_roasted          INTEGER NOT NULL REFERENCES biji_kopi_roasted (id_roasted),
    jumlah_gram         NUMERIC(10,2) NOT NULL,
    harga_per_gram      NUMERIC(12,2) NOT NULL,
    subtotal            NUMERIC(12,2) NOT NULL
);

-- ---------- Index untuk foreign key ----------

CREATE INDEX idx_biji_mentah_jenis    ON biji_kopi_mentah (id_jenis_biji_kopi_mentah);
CREATE INDEX idx_biji_mentah_metode   ON biji_kopi_mentah (id_metode_proses);
CREATE INDEX idx_batch_biji_mentah    ON batch_roasting (id_biji_mentah);
CREATE INDEX idx_batch_pengguna       ON batch_roasting (id_pengguna);
CREATE INDEX idx_roasted_batch        ON biji_kopi_roasted (id_batch);
CREATE INDEX idx_riwayat_biji_mentah  ON riwayat_stok (id_biji_mentah);
CREATE INDEX idx_riwayat_roasted      ON riwayat_stok (id_roasted);
CREATE INDEX idx_penjualan_pengguna   ON penjualan (id_pengguna);
CREATE INDEX idx_detail_penjualan     ON detail_penjualan (id_penjualan);
CREATE INDEX idx_detail_roasted       ON detail_penjualan (id_roasted);

-- ---------- Raw material validation ----------

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
