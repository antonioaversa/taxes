# This script should be run from the root of the repository.
# It downloads cryptocurrency market data from cryptodatadownload.com
# based on tickers found in the Basics.json file.

import json
import os
import re
import urllib.request
import urllib.error
import logging
import glob # For file pattern matching
from datetime import datetime
from typing import List, Dict, Any, Set

BASICS_JSON_PATH: str = "./Taxes/Reports/Basics.json"
OUTPUT_DIR: str = "./Taxes/Reports/"
LOG_FILE_PATH: str = os.path.join(OUTPUT_DIR, "MarketQuotes.log")
BASE_URL: str = "https://www.cryptodatadownload.com/cdd/"

# Logger will be configured in main() after potential old log deletion
logger = logging.getLogger("MarketQuotesDownloader")

def strip_json_comments(text: str) -> str:
    """Removes C-style comments from a JSON string."""
    text = re.sub(r"//.*?\n", "\n", text)
    text = re.sub(r"/\*.*?\*/", "", text, flags=re.DOTALL)
    return text

def get_crypto_tickers(basics_file_path: str) -> List[str]:
    """
    Reads Basics.json, strips comments, and extracts cryptocurrency tickers.
    """
    tickers: Set[str] = set()
    try:
        with open(basics_file_path, 'r') as f:
            file_content_with_comments: str = f.read()
        cleaned_json_content: str = strip_json_comments(file_content_with_comments)
        data: Dict[str, Any] = json.loads(cleaned_json_content)
        positions: Dict[str, Any] = data.get("Positions", {})
        for _symbol, details in positions.items():
            isin: str = details.get("ISIN", "")
            if isin.startswith("CRYPTO_") and len(isin) > len("CRYPTO_"):
                crypto_ticker: str = isin[len("CRYPTO_"):]
                tickers.add(crypto_ticker)
    except FileNotFoundError:
        msg = f"Error: {basics_file_path} not found."
        print(msg)
        # Logger might not be fully configured if this happens early
        if logger.hasHandlers(): logger.error(msg)
    except json.JSONDecodeError as e:
        msg = f"Error: Could not decode JSON from {basics_file_path}. Details: {e}"
        print(msg)
        if logger.hasHandlers(): logger.error(msg)
    except Exception as e:
        msg = f"An unexpected error occurred while reading {basics_file_path}: {e}"
        print(msg)
        if logger.hasHandlers(): logger.error(msg)
    return list(tickers)

def download_and_save_quotes(crypto_ticker: str, output_dir: str) -> bool:
    """
    Downloads daily crypto data for a given crypto_ticker against EUR, BTC, or ETH from multiple exchanges.
    Returns True if any download was successful, False otherwise.
    """
    exchanges: List[str] = ["Binance", "Coinbase", "Kraken"]
    target_quote_currencies: List[str] = ["EUR", "BTC", "ETH"]
    at_least_one_download_successful: bool = False

    for exchange in exchanges:
        for quote_currency in target_quote_currencies:
            if crypto_ticker.upper() == quote_currency.upper():
                # print(f"Skipping download for {crypto_ticker.upper()}{quote_currency.upper()} on {exchange} (same asset/invalid pair).")
                continue

            pair_name: str = f"{crypto_ticker.upper()}{quote_currency.upper()}"
            
            # Daily data parameters
            file_time_suffix: str = "_d" # For daily
            data_type_description: str = "daily"

            file_name_on_server: str = f"{exchange.capitalize()}_{pair_name}{file_time_suffix}.csv"
            download_url: str = f"{BASE_URL}{file_name_on_server}"
            # Output filename no longer needs _daily, as it's the only type
            output_file_name: str = f"MarketQuotes_{pair_name}.csv" 
            output_file_path: str = os.path.join(output_dir, output_file_name)
            
            attempt_msg = (f"Attempting: {data_type_description} data for {pair_name} from {exchange} "
                           f"via {download_url}...")
            print(attempt_msg)

            try:
                with urllib.request.urlopen(download_url, timeout=30) as response:
                    if 200 <= response.getcode() < 300:
                        content: bytes = response.read()
                        os.makedirs(output_dir, exist_ok=True)
                        
                        # Check and remove URL header if present
                        content_to_write = content
                        try:
                            first_newline_index = content.find(b'\n')
                            if first_newline_index != -1:
                                first_line_bytes = content[:first_newline_index]
                                # Attempt to decode the first line to check if it's a URL
                                first_line_str = first_line_bytes.decode('utf-8', errors='ignore').strip()
                                if first_line_str.lower().startswith("http://") or first_line_str.lower().startswith("https://"):
                                    content_to_write = content[first_newline_index+1:]
                                    print(f"Info: Removed URL header from {output_file_name}: {first_line_str}")
                                    if not content_to_write: # If file was only the URL line
                                        print(f"Info: File {output_file_name} was empty after removing URL header.")
                            elif content: # Content without newline, check if the whole thing is a URL
                                first_line_str = content.decode('utf-8', errors='ignore').strip()
                                if first_line_str.lower().startswith("http://") or first_line_str.lower().startswith("https://"):
                                    content_to_write = b'' # Empty content
                                    print(f"Info: Removed URL header (entire file content) from {output_file_name}: {first_line_str}")
                                    print(f"Info: File {output_file_name} is now empty after removing URL header.")

                        except Exception as e_strip:
                            # If any error occurs during stripping, log it and proceed with original content
                            logger.warning(f"Could not process/strip header for {output_file_name} due to: {e_strip}. Writing original content.")

                        # Check if file exists to log replacement
                        if os.path.exists(output_file_path):
                            logger.info(f"Replacing existing file: {output_file_path}")
                        
                        with open(output_file_path, 'wb') as f:
                            f.write(content_to_write)
                        success_msg = (f"SUCCESS: Downloaded {data_type_description} data for {pair_name} from {exchange}. "
                                       f"URL: {download_url}. Saved to: {output_file_name}")
                        print(success_msg)
                        logger.info(success_msg)
                        at_least_one_download_successful = True
                        # Since we successfully downloaded for this pair and exchange,
                        # and we only care about one successful download per crypto_ticker,
                        # we can break from the inner loops (quote_currency)
                        # and the outer loop (exchange) will also be broken by the return True below.
                        return True # Successfully downloaded for this crypto_ticker
                    else:
                        print(f"HTTP error for {data_type_description} {pair_name} on {exchange}: Status {response.getcode()}")
                        if response.getcode() == 404:
                            print(f"Info: {data_type_description.capitalize()} data not found (404) for {pair_name} on {exchange}.")
                        # For other HTTP errors, we continue to try next quote_currency or exchange
            except urllib.error.HTTPError as http_err:
                print(f"HTTPError for {data_type_description} {pair_name} on {exchange}: {http_err.code} - {http_err.reason}")
                if http_err.code == 404:
                    pass # Data not found, try next quote_currency or exchange
                else:
                    # For other HTTP errors, break from this exchange and try the next one
                    print(f"Breaking from {exchange} for {pair_name} due to non-404 HTTPError.")
                    break # Break from quote_currency loop, try next exchange
            except urllib.error.URLError as url_err:
                print(f"URLError for {data_type_description} {pair_name} on {exchange}: {url_err.reason}. Trying next exchange.")
                break # Break from quote_currency loop, try next exchange
            except Exception as e:
                print(f"Unexpected error for {data_type_description} {pair_name} on {exchange}: {e}. Trying next exchange.")
                break # Break from quote_currency loop, try next exchange
        
        # If at_least_one_download_successful is True here, it means the 'return True' inside the
        # quote_currency loop was hit, and this function would have already exited.
        # If we are here, it means all quote_currencies for the current 'exchange' failed or were skipped.
        # The outer 'exchange' loop will continue to the next exchange.

    # This point is reached if all exchanges and all their currency pairs failed to download
    if not at_least_one_download_successful:
        print(f"Failed to download any {data_type_description} data for {crypto_ticker.upper()} after all attempts.")
    return at_least_one_download_successful

def main() -> None:
    # Ensure OUTPUT_DIR exists before attempting to clean or log to it
    os.makedirs(OUTPUT_DIR, exist_ok=True)

    # --- Remove old log file ONLY. CSVs will be overwritten or left alone. ---
    print(f"Ensuring old log file from {OUTPUT_DIR} is removed...")
    
    log_file_cleaned = False
    if os.path.exists(LOG_FILE_PATH):
        try:
            os.remove(LOG_FILE_PATH)
            log_file_cleaned = True
            print(f"Removed old log file: {LOG_FILE_PATH}")
        except OSError as e:
            print(f"Error removing old log file {LOG_FILE_PATH}: {e}") 
    
    # --- Setup logging (after old log file, if any, is removed) ---
    logger.setLevel(logging.INFO)
    file_handler = logging.FileHandler(LOG_FILE_PATH) # Creates a new log file
    formatter = logging.Formatter('%(asctime)s - %(levelname)s - %(message)s')
    file_handler.setFormatter(formatter)
    logger.addHandler(file_handler)

    if log_file_cleaned:
        logger.info(f"Old log file {LOG_FILE_PATH} removed successfully.")
    else:
        logger.info(f"No old log file found at {LOG_FILE_PATH} or it could not be removed.")

    script_start_time = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    print(f"Starting crypto quote downloader at {script_start_time}...")
    logger.info(f"--- Crypto quote downloader script started at {script_start_time} ---")
    
    found_crypto_tickers: List[str] = get_crypto_tickers(BASICS_JSON_PATH)
    
    if not found_crypto_tickers:
        msg = "No crypto tickers found in Basics.json or error reading file."
        print(msg)
        logger.warning(msg)
        logger.info("--- Crypto quote downloader script finished ---")
        return
        
    console_msg = f"Found {len(found_crypto_tickers)} crypto ticker(s): {', '.join(found_crypto_tickers)}"
    print(console_msg)
    logger.info(console_msg)
    
    not_found_overall: List[str] = []

    for ticker in found_crypto_tickers:
        print("-" * 30)
        if not download_and_save_quotes(ticker, OUTPUT_DIR):
            not_found_overall.append(ticker)
            
    summary_header = "Download Summary:"
    print("\n" + "=" * 30)
    print(summary_header)
    logger.info(summary_header)

    if not not_found_overall:
        final_msg = "All crypto market quotes for all targeted tickers downloaded successfully."
        print(final_msg)
        logger.info(final_msg)
    else:
        fail_msg_console = (f"Could not find/download data for the following {len(not_found_overall)} "
                          f"ticker(s) after trying all exchanges/pairs:")
        logger.warning(fail_msg_console) # Log before printing to console
        print(fail_msg_console) 
        for ticker in not_found_overall:
            ticker_fail_msg = f"  - {ticker.upper()}"
            print(ticker_fail_msg)
            logger.warning(ticker_fail_msg)
            
    print("=" * 30)
    script_end_time = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    footer_msg = f"--- Crypto quote downloader script finished at {script_end_time} ---"
    print("Crypto quote downloader finished.")
    logger.info(footer_msg)
    
    # Important to remove handler to allow file to be cleanly handled next run if needed, esp. on Windows
    logger.removeHandler(file_handler)
    file_handler.close()

if __name__ == "__main__":
    main() 